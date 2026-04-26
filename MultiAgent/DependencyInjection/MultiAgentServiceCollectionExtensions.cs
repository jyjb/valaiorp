namespace Valaiorp.MultiAgent.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Memory.Implementations;
    using Valaiorp.MultiAgent.Contracts;
    using Valaiorp.MultiAgent.Orchestration;
    using Valaiorp.MultiAgent.Registry;

    public static class MultiAgentServiceCollectionExtensions
    {
        /// <summary>
        /// Registers multi-agent services: IAgentRegistry, IConversationMemory, MultiAgentOrchestrator.
        /// Call this after AddAgenticAIRuntime().
        /// </summary>
        public static IServiceCollection AddMultiAgentRuntime(
            this IServiceCollection services,
            int maxRounds = 20)
        {
            services.AddSingleton<IAgentRegistry, AgentRegistry>();
            services.AddSingleton<IConversationMemory, InMemoryConversationMemory>();
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
