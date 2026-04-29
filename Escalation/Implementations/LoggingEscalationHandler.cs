namespace Valaiorp.Escalation.Implementations
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// Writes escalation events to a TextWriter (default: Console.Error) and auto-resolves them.
    /// Swap for a notification-backed handler (Slack, Teams, PagerDuty) in production.
    /// </summary>
    public sealed class LoggingEscalationHandler : IEscalationHandler
    {
        private readonly TextWriter _writer;

        public LoggingEscalationHandler(TextWriter? writer = null)
            => _writer = writer ?? Console.Error;

        public async Task<EscalationResult> HandleEscalationAsync(
            IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default)
        {
            var line = $"[ESCALATION] {DateTimeOffset.UtcNow:o} | {reason} | context={context.Id}" +
                       (description != null ? $" | {description}" : string.Empty);

            await _writer.WriteLineAsync(line).ConfigureAwait(false);

            return new EscalationResult(true, "Logged", nameof(LoggingEscalationHandler));
        }
    }
}
