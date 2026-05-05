namespace Valaiorp.Execution.Models
{
    public sealed class ExecutionSnapshot
    {
        public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
        public string ContextId { get; init; } = string.Empty;
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public IReadOnlyList<StepSnapshot> Steps { get; init; } = [];

        public StepSnapshot? GetByIndex(int index)
            => index >= 0 && index < Steps.Count ? Steps[index] : null;

        public StepSnapshot? GetByName(string stepName)
            => Steps.FirstOrDefault(s =>
                string.Equals(s.StepName, stepName, StringComparison.OrdinalIgnoreCase));
    }
}
