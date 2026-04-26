namespace Valaiorp.Planner.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class Plan
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string ContextId { get; set; } = string.Empty;
        public IReadOnlyList<PlanStep> Steps { get; set; } = Array.Empty<PlanStep>();
        public DeterminismLevel Determinism { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }

    public sealed class PlanStep
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ToolId { get; set; }
        public string? ModuleId { get; set; }

        /// <summary>
        /// Target agent ID for multi-agent delegation.
        /// When set, the MultiAgentOrchestrator dispatches this step as an AgentMessage
        /// to the named agent instead of executing it with a local tool.
        /// </summary>
        public string? AgentId { get; set; }

        public IReadOnlyDictionary<string, object>? Inputs { get; set; }
        public IReadOnlyList<PlanStep> SubSteps { get; set; } = Array.Empty<PlanStep>();
        public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
        public int Priority { get; set; }
        public IReadOnlyDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
