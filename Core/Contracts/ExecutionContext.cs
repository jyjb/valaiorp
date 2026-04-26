namespace Valaiorp.Core.Contracts
{
    /// <summary>
    /// Default mutable implementation of IExecutionContext. Use this directly or subclass it.
    /// </summary>
    public class ExecutionContext : IExecutionContext
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ExpiresAt { get; init; }
        public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
        public IReadOnlyCollection<IExecutionStep> Steps { get; init; } = [];
        public CancellationToken CancellationToken { get; init; }
        public PromptContext? Prompt { get; set; }
    }
}
