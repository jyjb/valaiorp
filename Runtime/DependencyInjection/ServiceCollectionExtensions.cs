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
    using Valaiorp.Tools.Enhanced.Logging;
    using Valaiorp.Tools.Registries;
    using Valaiorp.Tools.Resolvers;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAgenticAIRuntime(
            this IServiceCollection services,
            AgenticAIConfig config)
        {
            // Core tool registries
            services.AddSingleton<ToolRegistry>();
            services.AddSingleton<ModuleRegistry>();
            services.AddSingleton<ToolResolver>();

            // Memory
            services.AddSingleton<IShortTermMemory, InMemoryShortTermMemory>();
            services.AddSingleton<ILongTermMemory, InMemoryLongTermMemory>();
            services.AddSingleton<IConversationMemory, InMemoryConversationMemory>();
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

            // Logging
            services.AddSingleton<IExecutionLogger, LocalExecutionLogger>();

            // Execution
            services.AddSingleton<ParallelExecutor>(sp =>
                new ParallelExecutor(
                    sp.GetRequiredService<ToolResolver>(),
                    sp.GetRequiredService<Valaiorp.Retry.Contracts.IRetryStrategy>(),
                    config.Parallelism.MaxDegreeOfParallelism));
            services.AddSingleton<TransactionManager>();

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
    }
}
