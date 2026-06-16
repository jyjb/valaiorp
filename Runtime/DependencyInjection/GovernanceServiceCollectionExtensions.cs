namespace Valaiorp.Runtime.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Valaiorp.Escalation.Contracts;
    using Valaiorp.Escalation.DependencyInjection;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Runtime.Governance;
    using Valaiorp.Tools.Governance;

    /// <summary>
    /// Opt-in registration for the mandatory tool-execution governance gate.
    /// <para>
    /// <see cref="ServiceCollectionExtensions.AddAgenticAIRuntime"/> installs the
    /// <see cref="UnwiredExecutionGate"/>, which refuses every tool call. Call
    /// <see cref="AddGovernance"/> (or <see cref="AddAutonomousGovernance"/>) afterwards to replace
    /// it with the real <see cref="DeterministicExecutionGate"/> so tools can run under policy.
    /// </para>
    /// </summary>
    public static class GovernanceServiceCollectionExtensions
    {
        /// <summary>
        /// Installs the deterministic execution gate (guardrails + optional high-risk approval),
        /// replacing the <see cref="UnwiredExecutionGate"/> registered by AddAgenticAIRuntime.
        /// </summary>
        /// <param name="services">The service collection (after AddAgenticAIRuntime).</param>
        /// <param name="configure">Optional callback to configure <see cref="GovernanceOptions"/>.</param>
        /// <exception cref="GovernanceNotWiredException">
        /// Thrown at configuration time when <see cref="GovernanceOptions.RequireApprovalForHighRisk"/>
        /// is enabled but no <see cref="IEscalationService"/> has been registered.
        /// </exception>
        public static IServiceCollection AddGovernance(
            this IServiceCollection services,
            Action<GovernanceOptions>? configure = null)
        {
            var options = new GovernanceOptions();
            configure?.Invoke(options);

            // Fail fast at startup: requiring approval without an approver would otherwise only
            // surface (fail-closed) on the first high-risk tool call at runtime.
            if (options.RequireApprovalForHighRisk &&
                !services.Any(d => d.ServiceType == typeof(IEscalationService)))
            {
                throw new GovernanceNotWiredException(
                    "GovernanceOptions.RequireApprovalForHighRisk is enabled but no IEscalationService " +
                    "is registered. Call services.AddEscalationServices() before AddGovernance, or use " +
                    "services.AddAutonomousGovernance(...) to auto-approve, or leave " +
                    "RequireApprovalForHighRisk false.");
            }

            services.AddSingleton(options);

            // Replace the UnwiredExecutionGate (or any prior gate) with the real one.
            services.RemoveAll<IExecutionGate>();
            services.AddSingleton<IExecutionGate>(sp => new DeterministicExecutionGate(
                sp.GetRequiredService<IGuardrailPipeline>(),
                sp.GetRequiredService<GovernanceOptions>(),
                sp.GetService<IEscalationService>()));

            return services;
        }

        /// <summary>
        /// Installs governance for unattended / autonomous hosts. Wires up auto-approving
        /// escalation services and then calls <see cref="AddGovernance"/>. Deterministic governance
        /// (policy, tool-scope, content guardrails, and audit) still applies in full — only the
        /// human approval step is auto-approved.
        /// </summary>
        public static IServiceCollection AddAutonomousGovernance(
            this IServiceCollection services,
            Action<GovernanceOptions>? configure = null)
        {
            services.AddEscalationServices(autoApprove: true);
            return services.AddGovernance(configure);
        }
    }
}
