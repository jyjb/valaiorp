namespace Valaiorp.MultiAgent.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public interface IAgentRegistry
    {
        void Register(IAgent agent, bool setAsDefaultOrchestrator = false);
        void Unregister(string agentId);

        IAgent? Resolve(string agentId);
        IAgent? GetOrchestrator();
        IReadOnlyCollection<IAgent> GetByRole(AgentRole role);

        /// <summary>Returns agents whose Capabilities list contains the given tool ID.</summary>
        IReadOnlyCollection<IAgent> GetByCapability(string toolId);

        IReadOnlyCollection<IAgent> All { get; }
    }
}
