namespace Valaiorp.Planner.Contracts
{
    using Valaiorp.Planner.Models;

    public interface IPlanEvaluator
    {
        PlanEvaluation Evaluate(Plan plan);
    }
}
