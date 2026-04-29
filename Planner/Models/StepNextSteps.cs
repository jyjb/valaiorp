namespace Valaiorp.Planner.Models
{
    public sealed class StepNextSteps
    {
        public IReadOnlyList<string> OnSuccess { get; set; } = [];
        public IReadOnlyList<string> OnFailure { get; set; } = [];
    }
}
