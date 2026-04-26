using Valaiorp.Core.Enums;

namespace Valaiorp.Core.Contracts
{
    public interface IExecutionStep
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        ExecutionMode Mode { get; }
        DeterminismLevel Determinism { get; }
        PlannerType Planner { get; }
        ToolType Tool { get; }
        DateTimeOffset StartedAt { get; }
        DateTimeOffset? CompletedAt { get; }
        bool IsCompleted { get; }
        string? Input { get; }
        string? Output { get; }
        string? Error { get; }
        IReadOnlyCollection<IExecutionStep> SubSteps { get; }
    }
}