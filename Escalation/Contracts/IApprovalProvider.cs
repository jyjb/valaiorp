namespace Valaiorp.Escalation.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Interface for approval providers. Implement this in your host application
    /// to integrate with your approval workflows (e.g., web UI, API, or service).
    /// </summary>
    public interface IApprovalProvider
    {
        /// <summary>
        /// Requests approval for an action in the given execution context.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="action">The action requiring approval.</param>
        /// <param name="description">Optional description of the action.</param>
        /// <param name="metadata">Optional metadata for the approval request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Approval result with status and details.</returns>
        Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Result of an approval request.
    /// </summary>
    public sealed class ApprovalResult
    {
        /// <summary>
        /// Whether the action was approved.
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// ID of the approver (e.g., user ID, system ID).
        /// </summary>
        public string? ApproverId { get; set; }

        /// <summary>
        /// Reason for approval or rejection.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Timestamp of the approval decision.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Additional metadata (e.g., audit data).
        /// </summary>
        public IDictionary<string, object>? Metadata { get; set; }

        public ApprovalResult(bool isApproved, string? approverId = null, string? reason = null, IDictionary<string, object>? metadata = null)
        {
            IsApproved = isApproved;
            ApproverId = approverId;
            Reason = reason;
            Metadata = metadata;
        }

        public static ApprovalResult Approved(string? approverId = null, string? reason = null, IDictionary<string, object>? metadata = null) =>
            new(true, approverId, reason, metadata);

        public static ApprovalResult Rejected(string? approverId = null, string? reason = null, IDictionary<string, object>? metadata = null) =>
            new(false, approverId, reason, metadata);
    }
}