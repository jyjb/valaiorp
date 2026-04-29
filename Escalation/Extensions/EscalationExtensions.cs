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
                return await service.RequestApprovalAsync(context, action, description, ct).ConfigureAwait(false);

            // No escalation service wired up: running in fully-autonomous mode.
            // Log every implicit approval so the decision is always auditable.
            WriteNoServiceAuditLog(context, action, description);
            return ApprovalResult.Approved("auto-approved", "No escalation service configured — fully-autonomous mode");
        }

        private static void WriteNoServiceAuditLog(IExecutionContext context, string action, string? description)
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "valaiorp-security.jsonl");
                var entry   = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ts        = DateTimeOffset.UtcNow,
                    provider  = "NoEscalationService",
                    decision  = "APPROVED",
                    mode      = "fully-autonomous",
                    contextId = context.Id,
                    sessionId = context.SessionId,
                    userId    = context.UserId,
                    action,
                    description
                });
                lock (_lock)
                    File.AppendAllText(logPath, entry + Environment.NewLine);
            }
            catch { }
        }

        private static readonly object _lock = new();

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
