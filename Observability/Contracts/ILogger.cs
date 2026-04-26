namespace Valaiorp.Observability.Contracts
{
    using System.Diagnostics;

    public interface ILogger
    {
        Task LogAsync(
            LogLevel level,
            string message,
            string? correlationId = null,
            Activity? activity = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default);
    }

    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }
}
