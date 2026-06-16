namespace Valaiorp.Governance.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Guardrails.BuiltIn;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Pipeline;
    using Valaiorp.Runtime.DependencyInjection;
    using Valaiorp.Runtime.Governance;
    using Valaiorp.Tools.Governance;
    using Valaiorp.Tools.Registries;
    using Valaiorp.Tools.Resolvers;
    using Xunit;
    using ExecutionContext = Valaiorp.Core.Contracts.ExecutionContext;

    public sealed class GovernanceGateTests
    {
        private static readonly IReadOnlyDictionary<string, object> NoParams =
            new Dictionary<string, object>();

        private static IExecutionContext Context() =>
            new ExecutionContext { SessionId = "s", UserId = "u" };

        /// <summary>Builds a resolver whose only registered tool is <paramref name="tool"/>.</summary>
        private static ToolResolver ResolverFor(IExecutionGate gate, RecordingTool tool)
        {
            var tools = new ToolRegistry();
            tools.Register(tool);
            return new ToolResolver(tools, new ModuleRegistry(), gate);
        }

        // (a) The default (unwired) gate refuses every tool call by throwing — it never executes.
        [Fact]
        public async Task UnwiredGate_Throws_GovernanceNotWired_AndDoesNotExecute()
        {
            var tool = new RecordingTool("any-tool");
            var resolver = ResolverFor(new UnwiredExecutionGate(), tool);

            var ex = await Assert.ThrowsAsync<GovernanceNotWiredException>(
                () => resolver.ExecuteToolAsync("any-tool", Context(), NoParams));

            Assert.Contains("AddGovernance", ex.Message);
            Assert.False(tool.WasExecuted);
        }

        // (b) A high-risk tool with a rejecting approver is denied and never executes.
        [Fact]
        public async Task HighRiskTool_WithRejectingApprover_DoesNotExecute()
        {
            var options = new GovernanceOptions { RequireApprovalForHighRisk = true };
            options.HighRiskToolIds.Add("delete-prod");
            var approver = new StubEscalationService(approve: false);
            var gate = new DeterministicExecutionGate(new GuardrailPipeline(), options, approver);

            var tool = new RecordingTool("delete-prod");
            var resolver = ResolverFor(gate, tool);

            var result = await resolver.ExecuteToolAsync("delete-prod", Context(), NoParams);

            Assert.False(result.IsSuccess);
            Assert.False(tool.WasExecuted);
            Assert.Equal(1, approver.ApprovalRequests);
            Assert.Contains("rejected", result.ErrorMessage);
        }

        // (c) A tool denied by the guardrail pipeline (tool-scope) never executes.
        [Fact]
        public async Task DeniedToolScope_DoesNotExecute()
        {
            var pipeline = new GuardrailPipeline();
            pipeline.Add(new ToolScopeGuardrail(deniedToolIds: new[] { "blocked-tool" }));
            var gate = new DeterministicExecutionGate(pipeline, new GovernanceOptions());

            var tool = new RecordingTool("blocked-tool");
            var resolver = ResolverFor(gate, tool);

            var result = await resolver.ExecuteToolAsync("blocked-tool", Context(), NoParams);

            Assert.False(result.IsSuccess);
            Assert.False(tool.WasExecuted);
            Assert.Contains("denied", result.ErrorMessage);
        }

        // (d) AddAutonomousGovernance replaces the unwired gate so a normal tool runs.
        [Fact]
        public async Task AddAutonomousGovernance_AllowsNormalTool_ToRun()
        {
            var services = new ServiceCollection();

            // Mirror the baseline the runtime establishes before governance is opted in.
            services.AddSingleton<ToolRegistry>();
            services.AddSingleton<ModuleRegistry>();
            services.AddSingleton<IGuardrailPipeline>(new GuardrailPipeline());
            services.AddSingleton<IExecutionGate, UnwiredExecutionGate>();
            services.AddSingleton<ToolResolver>(sp => new ToolResolver(
                sp.GetRequiredService<ToolRegistry>(),
                sp.GetRequiredService<ModuleRegistry>(),
                sp.GetRequiredService<IExecutionGate>()));

            // System under test.
            services.AddAutonomousGovernance();

            var provider = services.BuildServiceProvider();
            var tool = new RecordingTool("normal-tool");
            provider.GetRequiredService<ToolRegistry>().Register(tool);

            // The unwired gate must have been swapped for the deterministic one.
            Assert.IsType<DeterministicExecutionGate>(provider.GetRequiredService<IExecutionGate>());

            var resolver = provider.GetRequiredService<ToolResolver>();
            var result = await resolver.ExecuteToolAsync("normal-tool", Context(), NoParams);

            Assert.True(result.IsSuccess);
            Assert.True(tool.WasExecuted);
        }
    }
}
