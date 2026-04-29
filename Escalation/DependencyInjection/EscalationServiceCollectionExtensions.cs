namespace Valaiorp.Escalation.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;
    using Valaiorp.Escalation.Implementations;

    /// <summary>
    /// Extensions for registering escalation services in DI.
    /// </summary>
    public static class EscalationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers built-in escalation providers so the framework works out of the box.
        ///
        /// In production replace <see cref="IApprovalProvider"/> with a notification-backed
        /// implementation (Slack, Teams, email) by calling
        /// <c>services.AddSingleton&lt;IApprovalProvider, MyProvider&gt;()</c> after this call.
        /// </summary>
        /// <param name="autoApprove">
        /// When true, uses <see cref="AutoApproveProvider"/> (no human gate — suitable for tests/CI).
        /// When false (default), uses <see cref="ConsoleApprovalProvider"/> (blocks for operator input).
        /// </param>
        /// <param name="approvalTimeout">
        /// Timeout for console approval. Defaults to 5 minutes. Ignored when autoApprove is true.
        /// </param>
        public static IServiceCollection AddEscalationServices(
            this IServiceCollection services,
            bool autoApprove = false,
            TimeSpan? approvalTimeout = null)
        {
            IApprovalProvider approvalProvider = autoApprove
                ? new AutoApproveProvider()
                : new ConsoleApprovalProvider(approvalTimeout);

            services.AddSingleton<IApprovalProvider>(_ => approvalProvider);
            services.AddSingleton<IOverrideProvider, PassthroughOverrideProvider>();
            services.AddSingleton<IEscalationHandler>(new LoggingEscalationHandler());
            services.AddSingleton<IEscalationService>(sp => new DefaultEscalationService(
                sp.GetRequiredService<IApprovalProvider>(),
                sp.GetRequiredService<IOverrideProvider>(),
                sp.GetRequiredService<IEscalationHandler>()));

            return services;
        }

        /// <summary>
        /// Registers the escalation service interfaces with throw-factories so the host application
        /// must provide concrete implementations before the service can be resolved.
        /// Use <see cref="AddEscalationServices"/> instead for built-in implementations.
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