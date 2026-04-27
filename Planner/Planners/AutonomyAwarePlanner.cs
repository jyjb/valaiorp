namespace Valaiorp.Planner.Planners
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;

    /// <summary>
    /// Wraps any IPlanner and applies autonomy-level gating before returning the plan.
    /// Set context.Metadata["autonomy.allowDynamicPlanning"] = true to enable dynamic step injection.
    /// </summary>
    public sealed class AutonomyAwarePlanner : IPlanner
    {
        private readonly IPlanner _basePlanner;

        public AutonomyAwarePlanner(IPlanner basePlanner)
        {
            _basePlanner = basePlanner;
            Id = $"{basePlanner.Id}_autonomy_aware";
            Type = basePlanner.Type;
        }

        public string Id { get; }
        public PlannerType Type { get; }
        public DeterminismLevel Determinism { get; set; }

        public async Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            var basePlan = await _basePlanner.CreatePlanAsync(context, ct).ConfigureAwait(false);

            var allowDynamic = context.Metadata.TryGetValue("autonomy.allowDynamicPlanning", out var v)
                && v is true;

            return allowDynamic
                ? InjectDynamicStep(basePlan, context)
                : basePlan;
        }

        private static Plan InjectDynamicStep(Plan plan, IExecutionContext context)
        {
            var dynamic = new PlanStep
            {
                Name        = "DynamicStep",
                Description = "Injected at runtime based on autonomy level",
                Mode        = ExecutionMode.Sequential,
                Priority    = plan.Steps.Count + 1
            };

            return new Plan
            {
                ContextId      = plan.ContextId,
                Steps          = [.. plan.Steps, dynamic],
                Determinism    = plan.Determinism,
                PlanningTokens = plan.PlanningTokens
            };
        }
    }
}
