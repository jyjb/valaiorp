namespace Valaiorp.Escalation.Extensions
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// Extension methods for escalation in execution contexts.
    /// </summary>
    public static class EscalationExtensions
    {
        private const string EscalationServiceKey = "EscalationService";

        /// <summary>
        /// Sets the escalation service for the context.
        /// </summary>
        public static void SetEscalationService(this IExecutionContext context, IEscalationService service)
        {
            context.Metadata[EscalationServiceKey] = service;
        }

        /// <summary>
        /// Gets the escalation service from the context.
        /// </summary>
        public static IEscalationService? GetEscalationService(this IExecutionContext context)
        {
            return context.Metadata.TryGetValue(EscalationServiceKey, out var service)
                ? service as IEscalationService
                : null;
        }

        /// <summary>
        /// Requests approval for an action in the context.
        /// </summary>
        public static async Task<ApprovalResult> RequestApprovalAsync(
            this IExecutionContext context,
            string action,
            string? description = null,
            CancellationToken ct = default)
        {
            var service = context.GetEscalationService();
            if (service != null)
            {
                return await service.RequestApprovalAsync(context, action, description, ct).ConfigureAwait(false);
            }
            return ApprovalResult.Approved("auto-approved", "No escalation service configured");
        }

        /// <summary>
        /// Requests an override for an action in the context.
        /// </summary>
        public static async Task<OverrideResult> RequestOverrideAsync(
            this IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default)
        {
            var service = context.GetEscalationService();
            if (service != null)
            {
                return await service.RequestOverrideAsync(context, action, overrideReason, newParameters, ct)
                    .ConfigureAwait(false);
            }
            return new OverrideResult(false, null, action, action, newParameters);
        }

        /// <summary>
        /// Handles an escalation in the context.
        /// </summary>
        public static async Task<EscalationResult> EscalateAsync(
            this IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            CancellationToken ct = default)
        {
            var service = context.GetEscalationService();
            if (service != null)
            {
                return await service.HandleEscalationAsync(context, reason, description, ct).ConfigureAwait(false);
            }
            return new EscalationResult(false, "No escalation service configured");
        }
    }
}
