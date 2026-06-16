namespace Valaiorp.Governance.Tests
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Escalation.Contracts;
    using Valaiorp.Tools.Contracts;

    /// <summary>
    /// A tool that records whether it was actually executed, so tests can assert that a denied
    /// gate decision prevents execution rather than merely reporting failure afterward.
    /// </summary>
    internal sealed class RecordingTool : ITool
    {
        public RecordingTool(string id) => Id = id;

        public string Id { get; }
        public string Name => Id;
        public string Description => $"Recording test tool '{Id}'.";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        public bool WasExecuted { get; private set; }

        public Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            WasExecuted = true;
            return Task.FromResult(ToolResult.Ok(new { Tool = Id }));
        }
    }

    /// <summary>An <see cref="IEscalationService"/> whose approval outcome is fixed by the test.</summary>
    internal sealed class StubEscalationService : IEscalationService
    {
        private readonly bool _approve;
        public int ApprovalRequests { get; private set; }

        public StubEscalationService(bool approve) => _approve = approve;

        public Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            CancellationToken ct = default)
        {
            ApprovalRequests++;
            return Task.FromResult(_approve
                ? ApprovalResult.Approved("test", "approved by test")
                : ApprovalResult.Rejected("test", "rejected by test"));
        }

        public Task<OverrideResult> RequestOverrideAsync(
            IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<EscalationResult> HandleEscalationAsync(
            IExecutionContext context,
            EscalationReason reason,
            string? description = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
