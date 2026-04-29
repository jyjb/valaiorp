namespace Valaiorp.Planner.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class PlannerInfo
    {
        public PlannerType Type { get; set; } = PlannerType.Reactive;
        public TokenUsage? PlanningTokens { get; set; }
    }

    public sealed class PlanGovernance
    {
        public IReadOnlyList<string> PolicyChecks { get; set; } = ["PreExecution", "PostExecution"];
        public bool RequiresApproval { get; set; }
        public double RiskScore { get; set; }
    }

    public sealed class PlanMetadata
    {
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
    }

    public sealed class PlanDependencies
    {
        public IReadOnlyList<string> Tools { get; set; } = [];
        public IReadOnlyList<string> Modules { get; set; } = [];
    }
}
