namespace Valaiorp.Tools.Enhanced.Logging
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;

    public interface IExecutionLogger
    {
        Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default);
        Task LogRunAsync(ExecutionUnit unit, IExecutionContext context, CancellationToken ct = default);
        Task LogStepAsync(TaskNode node, IExecutionContext context, CancellationToken ct = default);
    }
}