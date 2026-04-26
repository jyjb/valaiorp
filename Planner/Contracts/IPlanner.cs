namespace Valaiorp.Planner.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Models;

    public interface IPlanner
    {
        string Id { get; }
        PlannerType Type { get; }
        DeterminismLevel Determinism { get; set; }
        Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default);
    }
}