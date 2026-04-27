namespace Valaiorp.Guardrails.Models
{
    using Valaiorp.Guardrails.Enums;

    public sealed class GuardrailResult
    {
        public bool IsAllowed { get; init; }
        public ViolationAction Action { get; init; }
        public string? GuardrailId { get; init; }
        public string? Reason { get; init; }

        /// <summary>
        /// Sanitized content returned when Action is Redact.
        /// The caller should use this in place of the original content.
        /// </summary>
        public string? SafeContent { get; init; }

        public IDictionary<string, object>? Metadata { get; init; }

        // ── Factory methods ───────────────────────────────────────────────────────

        public static GuardrailResult Allow(string? content = null) => new()
        {
            IsAllowed  = true,
            Action     = ViolationAction.Warn,
            SafeContent = content
        };

        public static GuardrailResult Block(string guardrailId, string reason) => new()
        {
            IsAllowed   = false,
            Action      = ViolationAction.Block,
            GuardrailId = guardrailId,
            Reason      = reason
        };

        public static GuardrailResult Redact(string guardrailId, string safeContent, string reason) => new()
        {
            IsAllowed   = true,
            Action      = ViolationAction.Redact,
            GuardrailId = guardrailId,
            SafeContent = safeContent,
            Reason      = reason
        };

        public static GuardrailResult Warn(string guardrailId, string reason,
            IDictionary<string, object>? metadata = null) => new()
        {
            IsAllowed   = true,
            Action      = ViolationAction.Warn,
            GuardrailId = guardrailId,
            Reason      = reason,
            Metadata    = metadata
        };

        public static GuardrailResult Escalate(string guardrailId, string reason) => new()
        {
            IsAllowed   = false,
            Action      = ViolationAction.Escalate,
            GuardrailId = guardrailId,
            Reason      = reason
        };
    }
}
