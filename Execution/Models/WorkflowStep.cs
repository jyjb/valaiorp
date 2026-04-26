namespace Valaiorp.Execution.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class WorkflowStep : IExecutionStep
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;
        public PlannerType Planner { get; set; } = PlannerType.None;
        public ToolType Tool { get; set; } = ToolType.None;
        public string? ToolId { get; set; }
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAt { get; set; }
        public bool IsCompleted { get; set; }
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public IReadOnlyCollection<IExecutionStep> SubSteps { get; set; } = Array.Empty<IExecutionStep>();

        public string? NextStepId { get; set; }
        public string? Condition { get; set; }
        public bool IsLoopStart { get; set; }
        public bool IsLoopEnd { get; set; }
        public string? LoopCondition { get; set; }
    }
}
