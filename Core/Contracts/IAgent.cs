namespace Valaiorp.Core.Contracts
{
    using Valaiorp.Core.Enums;

    public interface IAgent
    {
        string AgentId { get; }
        string Name { get; }
        string Description { get; }
        AgentRole Role { get; }

        /// <summary>Tool IDs this agent is permitted to use.</summary>
        IReadOnlyCollection<string> Capabilities { get; }

        Task<AgentResult> RunAsync(AgentMessage message, CancellationToken ct = default);
    }
}
