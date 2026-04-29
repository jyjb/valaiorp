namespace Valaiorp.Runtime.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Configuration.Config;
    using Valaiorp.Core.Abstractions;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Executors;
    using Valaiorp.Execution.Transactions;
    using Valaiorp.Knowledge.Resolvers;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Memory.Implementations;
    using Valaiorp.Observability.Contracts;
    using Valaiorp.Observability.Implementations;
    using Valaiorp.Observability.Metrics;
    using Valaiorp.Observability.Tracing;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Orchestration;
    using Valaiorp.Planner.Planners;
    using Valaiorp.Policy.Contracts;
    using Valaiorp.Policy.Engines;
    using Valaiorp.LlmProviders.DependencyInjection;
    using Valaiorp.Retry.DependencyInjection;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Modules;
    using Valaiorp.Tools.Enhanced.Logging;
    using Valaiorp.Tools.Registries;
    using Valaiorp.Tools.Resolvers;
    using Valaiorp.Guardrails.BuiltIn;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Pipeline;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAgenticAIRuntime(
            this IServiceCollection services,
            ValaiorpConfig config)
        {
            // Core tool registries
            services.AddSingleton<ToolRegistry>();
            services.AddSingleton<ModuleRegistry>();
            services.AddSingleton<ToolResolver>(sp => new ToolResolver(
                sp.GetRequiredService<ToolRegistry>(),
                sp.GetRequiredService<ModuleRegistry>(),
                module => new ModuleTool(module)));
            services.AddSingleton<ModuleExecutor>();

            // Memory — file-backed by default so state survives process restarts.
            // Swap for Redis/SQL implementations via services.AddSingleton<IShortTermMemory, ...>()
            // after calling AddAgenticAIRuntime().
            var memDir = config.Persistence.MemoryDirectory;
            services.AddSingleton<IShortTermMemory>(_ => new JsonlShortTermMemory(memDir));
            services.AddSingleton<ILongTermMemory>(_ => new JsonlLongTermMemory(memDir));
            services.AddSingleton<IConversationMemory>(_ => new JsonlConversationMemory(memDir));
            services.AddSingleton<MemoryManager>(sp => new MemoryManager(
                sp.GetRequiredService<IShortTermMemory>(),
                sp.GetRequiredService<ILongTermMemory>(),
                sp.GetRequiredService<IConversationMemory>()));

            // Knowledge
            services.AddSingleton<KnowledgeProviderResolver>();

            // Planner — InternalPlanner is always registered as the fallback.
            // If an ILlmClient is registered (e.g. via AddLlmClient()), the LlmPlanner
            // or CognitivePlanner is also registered and set as default by the orchestrator.
            services.AddSingleton<InternalPlanner>(sp => new InternalPlanner(
                sp.GetRequiredService<ToolRegistry>(),
                sp.GetRequiredService<ModuleRegistry>()));

            services.AddSingleton<PlannerOrchestrator>(sp =>
            {
                var orchestrator = new PlannerOrchestrator();

                // Always register the deterministic fallback
                orchestrator.RegisterPlanner(
                    sp.GetRequiredService<InternalPlanner>(),
                    setAsDefault: true);

                // If an ILlmClient has been registered, wire up the LLM-based planner
                var llmClient = sp.GetService<ILlmClient>();
                if (llmClient != null)
                {
                    var toolReg = sp.GetRequiredService<ToolRegistry>();
                    var modReg  = sp.GetRequiredService<ModuleRegistry>();

                    IPlanner llmPlanner = config.Planner.Type == Core.Enums.PlannerType.Deliberative
                        ? new CognitivePlanner(llmClient, toolReg, modReg)
                        : new LlmPlanner(llmClient, toolReg, modReg);

                    orchestrator.RegisterPlanner(llmPlanner, setAsDefault: true);
                }

                return orchestrator;
            });

            // Retry
            services.AddRetryModule();

            // Logging — ExternalExecutionLogger (JSONL) is mandatory and always registered.
            // Call services.AddSqlPersistence(factory) after this to also log to SQL,
            // which will replace the registration with a CompositeExecutionLogger.
            services.AddSingleton<IExecutionLogger>(
                _ => new ExternalExecutionLogger(config.Persistence.LogDirectory));

            // Execution
            services.AddSingleton<ParallelExecutor>(sp =>
            {
                // Fail fast at startup when approval is required but no approver is wired up.
                var escalation = sp.GetService<Valaiorp.Escalation.Contracts.IEscalationService>();
                if (config.Autonomy.RequireApprovalForHighRisk && escalation == null)
                    throw new InvalidOperationException(
                        "Autonomy.RequireApprovalForHighRisk is true but no IEscalationService is " +
                        "registered. Call services.AddEscalationInterfaces() and provide concrete " +
                        "implementations of IApprovalProvider, IOverrideProvider, and " +
                        "IEscalationHandler — or set RequireApprovalForHighRisk to false.");

                return new ParallelExecutor(
                    sp.GetRequiredService<ToolResolver>(),
                    sp.GetRequiredService<Valaiorp.Retry.Contracts.IRetryStrategy>(),
                    config.Parallelism.MaxDegreeOfParallelism,
                    config.Autonomy,
                    escalation,
                    sp.GetService<Valaiorp.Core.Contracts.IAgentRegistry>());
            });
            services.AddSingleton<TransactionManager>(sp =>
                new TransactionManager(sp.GetRequiredService<ToolResolver>()));

            // Guardrails
            services.AddSingleton<IGuardrailPipeline>(_ =>
            {
                var gc = config.Guardrails;
                var pipeline = new GuardrailPipeline();

                if (gc.EnablePiiRedaction)
                    pipeline.Add(new PiiGuardrail());

                if (gc.EnablePromptInjectionDetection)
                    pipeline.Add(new PromptInjectionGuardrail());

                if (gc.EnableBannedKeywords && gc.BannedKeywords?.Length > 0)
                    pipeline.Add(new BannedKeywordsGuardrail(gc.BannedKeywords));

                if (gc.MaxInputLengthChars > 0)
                    pipeline.Add(new ContentLengthGuardrail(GuardrailScope.Input, gc.MaxInputLengthChars));

                if (gc.MaxOutputLengthChars > 0)
                    pipeline.Add(new ContentLengthGuardrail(GuardrailScope.Output, gc.MaxOutputLengthChars));

                if (gc.AllowedToolIds?.Length > 0 || gc.DeniedToolIds?.Length > 0)
                    pipeline.Add(new ToolScopeGuardrail(gc.AllowedToolIds, gc.DeniedToolIds));

                if (gc.EnableDataClassification)
                    pipeline.Add(new DataClassificationGuardrail());

                return pipeline;
            });

            // Policy
            services.AddSingleton<IPolicyEngine, PolicyEngine>();

            // Observability
            services.AddSingleton<ILogger, ConsoleLogger>();
            services.AddSingleton<ExecutionTracer>();
            services.AddSingleton<MetricsCollector>();

            // LLM client — auto-wire from config if a provider is specified and no explicit
            // ILlmClient has already been registered by the caller.
            if (!string.IsNullOrWhiteSpace(config.Llm.Provider) &&
                !services.Any(d => d.ServiceType == typeof(ILlmClient)))
            {
                services.AddLlmClient(config.Llm);
            }

            // Config
            services.AddSingleton(config);

            // Runtime
            services.AddSingleton<AgentRuntime>();

            return services;
        }

        /// <summary>
        /// Adds SQL-backed execution logging alongside the mandatory local JSONL logger.
        /// The <paramref name="connectionFactory"/> should return a new, unopened DbConnection
        /// each time it is called (one per log write).
        ///
        /// Call this after AddAgenticAIRuntime:
        ///   services.AddAgenticAIRuntime(config)
        ///           .AddSqlPersistence(() => new SqlConnection(connStr));
        ///
        /// At app startup also call:
        ///   await sp.GetRequiredService&lt;SqlExecutionLogger&gt;().CreateSchemaAsync();
        /// </summary>
        public static IServiceCollection AddSqlPersistence(
            this IServiceCollection services,
            Func<System.Data.Common.DbConnection> connectionFactory)
        {
            // Capture the instance registered by AddAgenticAIRuntime — do NOT call its factory
            // again because that would create a second ExternalExecutionLogger writing to the
            // same JSONL files, causing interleaved/corrupt log records.
            var existingDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(IExecutionLogger));
            IExecutionLogger? capturedInstance = existingDescriptor?.ImplementationInstance as IExecutionLogger;

            services.AddSingleton<Valaiorp.Runtime.Logging.SqlExecutionLogger>(
                _ => new Valaiorp.Runtime.Logging.InlineSqlExecutionLogger(connectionFactory));

            // Remove the previous IExecutionLogger registration so the composite is the only one.
            if (existingDescriptor != null)
                services.Remove(existingDescriptor);

            services.AddSingleton<IExecutionLogger>(sp =>
            {
                // Use the already-constructed local logger if available; otherwise build it once.
                var local = capturedInstance
                    ?? (IExecutionLogger)new ExternalExecutionLogger(
                        sp.GetRequiredService<ValaiorpConfig>().Persistence.LogDirectory);

                var sql = sp.GetRequiredService<Valaiorp.Runtime.Logging.SqlExecutionLogger>();
                return new Valaiorp.Tools.Enhanced.Logging.CompositeExecutionLogger([local, sql]);
            });

            return services;
        }
    }
}
