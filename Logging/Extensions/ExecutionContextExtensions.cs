namespace Valaiorp.Tools.Enhanced.Extensions
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Enhanced.Logging;

    public static class ExecutionContextExtensions
    {
        private const string LoggerKey = "ExecutionLogger";

        public static void SetLogger(this IExecutionContext context, IExecutionLogger logger)
        {
            context.Metadata[LoggerKey] = logger;
        }

        public static IExecutionLogger? GetLogger(this IExecutionContext context)
        {
            return context.Metadata.TryGetValue(LoggerKey, out var logger) ? logger as IExecutionLogger : null;
        }

        public static async Task LogPlanAsync(this IExecutionContext context, Plan plan, CancellationToken ct = default)
        {
            var logger = context.GetLogger();
            if (logger != null)
            {
                await logger.LogPlanAsync(plan, context, ct).ConfigureAwait(false);
            }
        }

        public static async Task LogRunAsync(this IExecutionContext context, ExecutionUnit unit, CancellationToken ct = default)
        {
            var logger = context.GetLogger();
            if (logger != null)
            {
                await logger.LogRunAsync(unit, context, ct).ConfigureAwait(false);
            }
        }

        public static async Task LogStepAsync(this IExecutionContext context, TaskNode node, CancellationToken ct = default)
        {
            var logger = context.GetLogger();
            if (logger != null)
            {
                await logger.LogStepAsync(node, context, ct).ConfigureAwait(false);
            }
        }
    }
}