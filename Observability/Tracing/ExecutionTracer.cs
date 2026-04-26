namespace Valaiorp.Observability.Tracing
{
    using System.Diagnostics;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Observability.Contracts;

    public sealed class ExecutionTracer
    {
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;

        public ExecutionTracer(ILogger logger, string activitySourceName = "NaraiNirai.AgenticAI")
        {
            _logger = logger;
            _activitySource = new ActivitySource(activitySourceName);
        }

        public async Task<T> TraceAsync<T>(
            string operationName,
            string? correlationId,
            Func<Activity?, CancellationToken, Task<T>> operation,
            CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity(operationName);
            activity?.SetTag("correlationId", correlationId ?? string.Empty);

            try
            {
                await _logger.LogAsync(
                    LogLevel.Information,
                    $"Starting operation: {operationName}",
                    correlationId,
                    activity,
                    ct: ct)
                .ConfigureAwait(false);

                var result = await operation(activity, ct).ConfigureAwait(false);

                await _logger.LogAsync(
                    LogLevel.Information,
                    $"Completed operation: {operationName}",
                    correlationId,
                    activity,
                    ct: ct)
                .ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    LogLevel.Error,
                    $"Failed operation: {operationName}. Error: {ex.Message}",
                    correlationId,
                    activity,
                    new Dictionary<string, object> { { "Exception", ex } },
                    ct)
                .ConfigureAwait(false);

                throw;
            }
        }

        public async Task TraceAsync(
            string operationName,
            string? correlationId,
            Func<Activity?, CancellationToken, Task> operation,
            CancellationToken ct = default)
        {
            await TraceAsync(operationName, correlationId, async (activity, token) =>
            {
                await operation(activity, token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
        }
    }
}