namespace Valaiorp.MultiAgent.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.MultiAgent.Orchestration;
    using Valaiorp.MultiAgent.Registry;

    public static class MultiAgentServiceCollectionExtensions
    {
        /// <summary>
        /// Registers multi-agent services: IAgentRegistry and MultiAgentOrchestrator.
        /// Call this after AddAgenticAIRuntime() — IConversationMemory is already registered
        /// there and is reused here rather than duplicated.
        /// </summary>
        public static IServiceCollection AddMultiAgentRuntime(
            this IServiceCollection services,
            int maxRounds = 20)
        {
            // IConversationMemory is registered by AddAgenticAIRuntime; reuse it.
            // Registering again would create a second file-backed instance writing to the same files.
            if (!services.Any(d => d.ServiceType == typeof(IAgentRegistry)))
                services.AddSingleton<IAgentRegistry, AgentRegistry>();

            services.AddSingleton(sp => new MultiAgentOrchestrator(
                sp.GetRequiredService<IAgentRegistry>(),
                sp.GetRequiredService<IConversationMemory>())
            {
                MaxRounds = maxRounds
            });

            return services;
        }
    }
}
