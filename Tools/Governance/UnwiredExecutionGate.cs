namespace Valaiorp.Tools.Governance
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// The default <see cref="IExecutionGate"/> registered by the runtime. It refuses every
    /// tool call by throwing <see cref="GovernanceNotWiredException"/>, forcing the developer
    /// to explicitly opt into a real governance policy before any tool can run.
    /// Replace it by calling <c>services.AddGovernance(...)</c> (and
    /// <c>services.AddEscalationServices()</c> when human approval is required).
    /// </summary>
    public sealed class UnwiredExecutionGate : IExecutionGate
    {
        public Task<GateDecision> AuthorizeAsync(
            string toolId,
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            throw new GovernanceNotWiredException(
                $"Tool '{toolId}' was blocked because governance is not wired up. " +
                "Tool execution is gated and refuses to run without an explicit policy. " +
                "Call services.AddGovernance(...) during startup to install a real execution gate " +
                "(and services.AddEscalationServices() if any tool requires human approval). " +
                "For unattended/autonomous hosts use services.AddAutonomousGovernance(...).");
        }
    }
}
