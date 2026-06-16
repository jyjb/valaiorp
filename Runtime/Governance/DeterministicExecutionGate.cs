namespace Valaiorp.Runtime.Governance
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Tools.Governance;

    /// <summary>
    /// The real <see cref="IExecutionGate"/> installed by <c>AddGovernance(...)</c>. It applies
    /// two deterministic checks before any tool runs:
    /// <list type="number">
    ///   <item>the guardrail pipeline's tool-call evaluation (policy / tool-scope / content); and</item>
    ///   <item>human approval for high-risk tools, when configured.</item>
    /// </list>
    /// The gate fails closed: exceptions thrown by the guardrails or the escalation service
    /// propagate and prevent the tool from executing.
    /// </summary>
    public sealed class DeterministicExecutionGate : IExecutionGate
    {
        private readonly IGuardrailPipeline _guardrails;
        private readonly GovernanceOptions _options;
        private readonly IEscalationService? _escalation;

        public DeterministicExecutionGate(
            IGuardrailPipeline guardrails,
            GovernanceOptions options,
            IEscalationService? escalation = null)
        {
            _guardrails = guardrails ?? throw new ArgumentNullException(nameof(guardrails));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _escalation = escalation;
        }

        public async Task<GateDecision> AuthorizeAsync(
            string toolId,
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            // (a) Deterministic guardrails: policy, tool-scope, content classification, etc.
            var guardrail = await _guardrails
                .EvaluateToolCallAsync(context, toolId, parameters, ct)
                .ConfigureAwait(false);

            if (!guardrail.IsAllowed)
                return GateDecision.Deny(
                    guardrail.Reason ?? $"Tool '{toolId}' was blocked by a guardrail.");

            // (b) Human approval for high-risk tools.
            if (_options.RequireApprovalForHighRisk && _options.HighRiskToolIds.Contains(toolId))
            {
                if (_escalation is null)
                    throw new GovernanceNotWiredException(
                        $"Tool '{toolId}' is configured as high-risk and requires human approval, " +
                        "but no IEscalationService is registered. Call services.AddEscalationServices() " +
                        "(or services.AddAutonomousGovernance(...) to auto-approve) before AddGovernance.");

                var approval = await _escalation
                    .RequestApprovalAsync(context, toolId, ct: ct)
                    .ConfigureAwait(false);

                if (!approval.IsApproved)
                    return GateDecision.Deny(
                        approval.Reason ?? $"Approval was denied for high-risk tool '{toolId}'.");
            }

            return GateDecision.Allow();
        }
    }
}
