namespace Valaiorp.Escalation.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// High-level service for managing escalations, approvals, and overrides.
    /// Implement this in your host application to coordinate between providers.
    /// </summary>
    public interface IEscalationService
    {
        /// <summary>
        /// Requests approval for an action.
        /// </summary>
        Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            CancellationToken ct = default);

        /// <summary>
        /// Requests an override for an action.
        /// </summary>
        Task<OverrideResult> RequestOverrideAsync(
            IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default);

        /// <summary>
        /// Handles an escalation.
        /// </summary>
        Task<EscalationResult> HandleEscalationAsync(
            IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            CancellationToken ct = default);
    }
}