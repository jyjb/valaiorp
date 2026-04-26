namespace Valaiorp.Observability.Implementations
{
    using System.Diagnostics;
    using Valaiorp.Observability.Contracts;

    public sealed class ConsoleLogger : ILogger
    {
        public async Task LogAsync(
            LogLevel level,
            string message,
            string? correlationId = null,
            Activity? activity = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default)
        {
            await Task.Yield();

            var timestamp = DateTimeOffset.UtcNow.ToString("o");
            var levelStr = level.ToString().ToUpperInvariant();
            var correlationIdStr = string.IsNullOrEmpty(correlationId) ? "-" : correlationId;

            var metadataStr = metadata != null && metadata.Count > 0
                ? $" | {string.Join(", ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"
                : string.Empty;

            Console.WriteLine($"{timestamp} [{levelStr}] {correlationIdStr} - {message}{metadataStr}");
        }
    }
}
