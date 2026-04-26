namespace Valaiorp.Configuration.Models
{
    using Valaiorp.Core.Enums;

    public sealed class PlannerConfig
    {
        public PlannerType Type { get; set; } = PlannerType.Reactive;
        public int MaxDepth { get; set; } = 10;
        public int MaxBranchingFactor { get; set; } = 5;
        public bool EnableBacktracking { get; set; } = true;
        public TimeSpan PlanningTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}