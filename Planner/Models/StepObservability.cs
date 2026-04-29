namespace Valaiorp.Planner.Models
{
    public sealed class StepObservability
    {
        public string LogLevel { get; set; } = "Information";
        public IReadOnlyList<string> Metrics { get; set; } = [];
        public bool Trace { get; set; } = true;
    }
}
