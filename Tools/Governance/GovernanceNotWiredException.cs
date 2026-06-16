namespace Valaiorp.Tools.Governance
{
    /// <summary>
    /// Thrown when a tool call reaches the execution chokepoint but no real governance gate
    /// has been wired up. Signals a deployment/configuration error rather than a runtime
    /// authorization failure — the framework fails closed instead of silently executing tools
    /// without governance.
    /// </summary>
    public sealed class GovernanceNotWiredException : InvalidOperationException
    {
        public GovernanceNotWiredException(string message)
            : base(message)
        {
        }

        public GovernanceNotWiredException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
