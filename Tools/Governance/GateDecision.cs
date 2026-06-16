namespace Valaiorp.Tools.Governance
{
    /// <summary>
    /// The outcome of an <see cref="IExecutionGate"/> authorization check for a single tool call.
    /// </summary>
    public sealed class GateDecision
    {
        /// <summary>When true the tool call may proceed; when false it must not execute.</summary>
        public bool IsAllowed { get; }

        /// <summary>Human-readable explanation, surfaced as the ExecutionResult error when denied.</summary>
        public string? Reason { get; }

        private GateDecision(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>The tool call is authorized.</summary>
        public static GateDecision Allow() => new(true, null);

        /// <summary>The tool call is rejected with the supplied reason.</summary>
        public static GateDecision Deny(string reason) => new(false, reason);
    }
}
