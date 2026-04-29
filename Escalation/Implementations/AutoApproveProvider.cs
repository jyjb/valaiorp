namespace Valaiorp.Escalation.Implementations
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// Always approves. Use in automated / fully-agentic pipelines where human-in-the-loop
    /// is intentionally disabled. Every approval is written to the security audit log so the
    /// decision is always traceable.
    /// Do NOT use in supervised or production deployments without understanding the implications.
    /// </summary>
    public sealed class AutoApproveProvider : IApprovalProvider
    {
        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "valaiorp-security.jsonl");

        private static readonly object _lock = new();

        public Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default)
        {
            WriteAuditLog(context, action, description, metadata);
            return Task.FromResult(ApprovalResult.Approved("auto", "AutoApproveProvider"));
        }

        private static void WriteAuditLog(
            IExecutionContext context,
            string action,
            string? description,
            IDictionary<string, object>? metadata)
        {
            try
            {
                var entry = JsonSerializer.Serialize(new
                {
                    ts          = DateTimeOffset.UtcNow,
                    provider    = nameof(AutoApproveProvider),
                    decision    = "APPROVED",
                    contextId   = context.Id,
                    sessionId   = context.SessionId,
                    userId      = context.UserId,
                    action,
                    description,
                    metadataKeys = metadata?.Keys.ToArray()
                });

                lock (_lock)
                    File.AppendAllText(LogPath, entry + Environment.NewLine);
            }
            catch { }
        }
    }
}
