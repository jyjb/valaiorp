namespace Valaiorp.Execution.Models
{
    public sealed class StepSnapshot
    {
        public string StepId { get; init; } = string.Empty;
        public string StepName { get; init; } = string.Empty;
        public int StepIndex { get; init; }
        public string? Input { get; init; }
        public string? Output { get; init; }
        public string? Error { get; init; }
        public bool IsSuccess { get; init; }
        public TimeSpan Duration { get; init; }
        public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
        public IReadOnlyDictionary<string, object> WorkflowStateSnapshot { get; init; }
            = new Dictionary<string, object>();
    }
}
