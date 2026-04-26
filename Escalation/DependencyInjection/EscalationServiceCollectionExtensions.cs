namespace Valaiorp.Escalation.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// Extensions for registering escalation services in DI.
    /// The host application MUST provide implementations for these interfaces.
    /// </summary>
    public static class EscalationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the escalation service interfaces. The host application
        /// must provide concrete implementations.
        /// </summary>
        public static IServiceCollection AddEscalationInterfaces(this IServiceCollection services)
        {
            // Register interfaces only. The host app must implement these.
            services.AddSingleton<IApprovalProvider>(provider =>
                throw new InvalidOperationException(
                    "No IApprovalProvider implementation registered. " +
                    "Please register an implementation in your host application."));

            services.AddSingleton<IOverrideProvider>(provider =>
                throw new InvalidOperationException(
                    "No IOverrideProvider implementation registered. " +
                    "Please register an implementation in your host application."));

            services.AddSingleton<IEscalationHandler>(provider =>
                throw new InvalidOperationException(
                    "No IEscalationHandler implementation registered. " +
                    "Please register an implementation in your host application."));

            services.AddSingleton<IEscalationService>(provider =>
                new DefaultEscalationService(
                    provider.GetRequiredService<IApprovalProvider>(),
                    provider.GetRequiredService<IOverrideProvider>(),
                    provider.GetRequiredService<IEscalationHandler>()));

            return services;
        }
    }

    /// <summary>
    /// Default implementation of IEscalationService that coordinates between
    /// IApprovalProvider, IOverrideProvider, and IEscalationHandler.
    /// </summary>
    internal sealed class DefaultEscalationService : IEscalationService
    {
        private readonly IApprovalProvider _approvalProvider;
        private readonly IOverrideProvider _overrideProvider;
        private readonly IEscalationHandler _escalationHandler;

        public DefaultEscalationService(
            IApprovalProvider approvalProvider,
            IOverrideProvider overrideProvider,
            IEscalationHandler escalationHandler)
        {
            _approvalProvider = approvalProvider;
            _overrideProvider = overrideProvider;
            _escalationHandler = escalationHandler;
        }

        public Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            CancellationToken ct = default)
        {
            return _approvalProvider.RequestApprovalAsync(context, action, description, null, ct);
        }

        public Task<OverrideResult> RequestOverrideAsync(
            IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default)
        {
            return _overrideProvider.OverrideAsync(context, action, overrideReason, newParameters, ct);
        }

        public Task<EscalationResult> HandleEscalationAsync(
            IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            CancellationToken ct = default)
        {
            return _escalationHandler.HandleEscalationAsync(context, reason, description, null, ct);
        }
    }
}