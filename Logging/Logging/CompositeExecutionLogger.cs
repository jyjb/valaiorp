namespace Valaiorp.Tools.Enhanced.Logging
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;

    /// <summary>
    /// Fans out every log call to all registered loggers in parallel.
    /// Used when both local JSONL and SQL logging are active simultaneously.
    /// </summary>
    public sealed class CompositeExecutionLogger : IExecutionLogger
    {
        private readonly IReadOnlyList<IExecutionLogger> _loggers;

        public CompositeExecutionLogger(IEnumerable<IExecutionLogger> loggers)
        {
            _loggers = loggers.ToList();
        }

        public Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default)
            => Task.WhenAll(_loggers.Select(l => l.LogPlanAsync(plan, context, ct)));

        public Task LogRunAsync(ExecutionUnit unit, IExecutionContext context, CancellationToken ct = default)
            => Task.WhenAll(_loggers.Select(l => l.LogRunAsync(unit, context, ct)));

        public Task LogStepAsync(TaskNode node, IExecutionContext context, CancellationToken ct = default)
            => Task.WhenAll(_loggers.Select(l => l.LogStepAsync(node, context, ct)));
    }
}
