namespace Valaiorp.Runtime.Bootstrap
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Configuration.Config;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Runtime.Bot;
    using Valaiorp.Runtime.Configuration;
    using Valaiorp.Runtime.DependencyInjection;
    using Valaiorp.Runtime.Queue;
    using Valaiorp.BasicTools.Registries;
    using Valaiorp.Tools.Registries;

    public static class RuntimeBuilder
    {
        // ── Single-runtime factory ────────────────────────────────────────────────

        public static AgentRuntime Build(
            ValaiorpConfig? config = null,
            Action<IServiceCollection>? configureServices = null,
            Action<ModuleRegistry>? configureModules = null)
        {
            var services = new ServiceCollection();

            var effectiveConfig = config ?? RuntimeConfigLoader.LoadDefault();
            effectiveConfig.Validate();
            services.AddAgenticAIRuntime(effectiveConfig);

            configureServices?.Invoke(services);

            var provider = services.BuildServiceProvider();
            BasicToolsRegistry.RegisterAll(provider.GetRequiredService<ToolRegistry>());
            configureModules?.Invoke(provider.GetRequiredService<ModuleRegistry>());
            return provider.GetRequiredService<AgentRuntime>();
        }

        public static AgentRuntime BuildFromFile(
            string configFilePath,
            Action<IServiceCollection>? configureServices = null,
            Action<ModuleRegistry>? configureModules = null)
        {
            var config = RuntimeConfigLoader.LoadFromFile(configFilePath);
            return Build(config, configureServices, configureModules);
        }

        // ── Multi-bot / queue factory ─────────────────────────────────────────────

        /// <summary>
        /// Builds a BotWorker — a self-contained, continuously running bot that pulls
        /// IWorkItems from a shared IWorkQueue and executes each one through AgentRuntime.
        ///
        /// Deploy N BotWorkers across N machines pointing at the same IWorkQueue backend
        /// (swap InMemoryWorkQueue for a SQL / Service Bus / RabbitMQ implementation).
        ///
        /// Call worker.Start() after building. Dispose with await worker.DisposeAsync().
        /// </summary>
        /// <param name="botId">Logical bot name shared across all instances of this bot type.</param>
        /// <param name="config">
        ///   Workflow config. Call config.ApplyProfile() before passing, or let BuildBot do it.
        ///   WorkflowType and AiParticipation are applied automatically.
        /// </param>
        /// <param name="queue">
        ///   Shared queue. Defaults to InMemoryWorkQueue (single-process only).
        ///   For multi-machine, pass a shared broker-backed implementation.
        /// </param>
        /// <param name="contextFactory">
        ///   Converts an IWorkItem into an IExecutionContext for the runtime.
        ///   Defaults to an ExecutionContext with SessionId=WorkflowId, UserId=botId,
        ///   Metadata populated from the item's Payload.
        /// </param>
        /// <param name="maxConcurrency">Max items processed in parallel per BotWorker instance.</param>
        /// <param name="maxAttempts">Max retry attempts before dead-lettering a failed item.</param>
        public static BotWorker BuildBot(
            string                             queueId,
            string                             botId,
            ValaiorpConfig?                   config            = null,
            IWorkQueue?                        queue             = null,
            Func<IWorkItem, IExecutionContext>? contextFactory   = null,
            Action<IServiceCollection>?        configureServices = null,
            Action<ModuleRegistry>?            configureModules  = null,
            int                                maxConcurrency    = 4,
            int                                maxAttempts       = 3)
        {
            var effectiveConfig = (config ?? RuntimeConfigLoader.LoadDefault()).ApplyProfile();
            var runtime         = Build(effectiveConfig, configureServices, configureModules);
            var effectiveQueue  = queue ?? new JsonlWorkQueue(effectiveConfig.Persistence.QueueDirectory);

            var effectiveFactory = contextFactory ?? (item => new ExecutionContext
            {
                SessionId = item.QueueId,
                UserId    = botId,
                Metadata  = new Dictionary<string, object>(item.Payload)
            });

            var botContext = new BotContext
            {
                BotId        = botId,
                WorkflowType = effectiveConfig.WorkflowType
            };

            return new BotWorker(runtime, effectiveQueue, botContext, queueId,
                effectiveFactory, maxConcurrency, maxAttempts);
        }
    }
}
