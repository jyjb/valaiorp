namespace Valaiorp.Guardrails.Enums
{
    public enum ViolationAction
    {
        /// <summary>Reject the content. Execution stops and an error is returned to the caller.</summary>
        Block,

        /// <summary>Sanitize the content and continue execution with the safe version.</summary>
        Redact,

        /// <summary>Allow the content but emit a warning for audit purposes.</summary>
        Warn,

        /// <summary>Block and route to the escalation handler for human review.</summary>
        Escalate
    }
}
