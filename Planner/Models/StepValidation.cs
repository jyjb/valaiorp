namespace Valaiorp.Planner.Models
{
    public sealed class StepValidation
    {
        public IReadOnlyList<StepCondition> PreConditions { get; set; } = [];
        public IReadOnlyList<StepCondition> PostConditions { get; set; } = [];
    }

    public sealed class StepCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Expression { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        /// <summary>Step IDs this condition checks for completion (StepDependencyCheck type).</summary>
        public IReadOnlyList<string>? DependsOn { get; set; }
    }
}
