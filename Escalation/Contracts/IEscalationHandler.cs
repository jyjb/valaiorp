namespace Valaiorp.Escalation.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Interface for escalation handlers. Implement this in your host application
    /// to handle escalations (e.g., high-risk actions, policy violations).
    /// </summary>
    public interface IEscalationHandler
    {
        /// <summary>
        /// Handles an escalation event.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="reason">Reason for escalation.</param>
        /// <param name="description">Optional description of the escalation.</param>
        /// <param name="metadata">Optional metadata for the escalation.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Escalation result with resolution status.</returns>
        Task<EscalationResult> HandleEscalationAsync(
            IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Reason for escalation.
    /// </summary>
    public enum EscalationReason
    {
        /// <summary>
        /// High-risk action detected.
        /// </summary>
        HighRiskAction,

        /// <summary>
        /// Policy violation detected.
        /// </summary>
        PolicyViolation,

        /// <summary>
        /// Uncertain decision (e.g., low confidence).
        /// </summary>
        UncertainDecision,

        /// <summary>
        /// Manual intervention requested.
        /// </summary>
        ManualInterventionRequested
    }

    /// <summary>
    /// Result of an escalation handling.
    /// </summary>
    public sealed class EscalationResult
    {
        /// <summary>
        /// Whether the escalation was resolved.
        /// </summary>
        public bool IsResolved { get; set; }

        /// <summary>
        /// Resolution description.
        /// </summary>
        public string? Resolution { get; set; }

        /// <summary>
        /// ID of the handler (e.g., for auditing).
        /// </summary>
        public string? HandlerId { get; set; }

        /// <summary>
        /// Timestamp of the resolution.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Additional metadata (e.g., approval or override details).
        /// </summary>
        public IDictionary<string, object>? Metadata { get; set; }

        public EscalationResult(bool isResolved, string? resolution = null, string? handlerId = null, IDictionary<string, object>? metadata = null)
        {
            IsResolved = isResolved;
            Resolution = resolution;
            HandlerId = handlerId;
            Metadata = metadata;
        }
    }
}
