namespace Valaiorp.Runtime.Governance
{
    /// <summary>
    /// Configuration for the <see cref="DeterministicExecutionGate"/>.
    /// </summary>
    public sealed class GovernanceOptions
    {
        /// <summary>
        /// When true, any tool whose id is listed in <see cref="HighRiskToolIds"/> requires
        /// human approval (via <c>IEscalationService</c>) before it is allowed to execute.
        /// </summary>
        public bool RequireApprovalForHighRisk { get; set; }

        /// <summary>
        /// Tool ids that are considered high-risk. Compared case-insensitively.
        /// </summary>
        public HashSet<string> HighRiskToolIds { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
