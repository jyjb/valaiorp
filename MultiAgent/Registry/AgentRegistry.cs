namespace Valaiorp.MultiAgent.Registry
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.MultiAgent.Contracts;

    public sealed class AgentRegistry : IAgentRegistry
    {
        private readonly ConcurrentDictionary<string, IAgent> _agents = new();
        private string _defaultOrchestratorId = string.Empty;

        public IReadOnlyCollection<IAgent> All => [.. _agents.Values];

        public void Register(IAgent agent, bool setAsDefaultOrchestrator = false)
        {
            _agents.AddOrUpdate(agent.AgentId, agent, (_, _) => agent);

            if (setAsDefaultOrchestrator ||
                (string.IsNullOrEmpty(_defaultOrchestratorId) && agent.Role == AgentRole.Orchestrator))
            {
                _defaultOrchestratorId = agent.AgentId;
            }
        }

        public void Unregister(string agentId)
        {
            _agents.TryRemove(agentId, out _);
            if (_defaultOrchestratorId == agentId)
                _defaultOrchestratorId = string.Empty;
        }

        public IAgent? Resolve(string agentId)
            => _agents.TryGetValue(agentId, out var a) ? a : null;

        public IAgent? GetOrchestrator()
        {
            if (!string.IsNullOrEmpty(_defaultOrchestratorId) &&
                _agents.TryGetValue(_defaultOrchestratorId, out var a))
                return a;

            return _agents.Values.FirstOrDefault(x => x.Role == AgentRole.Orchestrator);
        }

        public IReadOnlyCollection<IAgent> GetByRole(AgentRole role)
            => [.. _agents.Values.Where(a => a.Role == role)];

        public IReadOnlyCollection<IAgent> GetByCapability(string toolId)
            => [.. _agents.Values.Where(a => a.Capabilities.Contains(toolId))];
    }
}
