namespace Valaiorp.Core.Contracts
{
    public interface IExecutionContext
    {
        string Id { get; }
        string SessionId { get; }
        string UserId { get; }
        DateTimeOffset CreatedAt { get; }
        DateTimeOffset? ExpiresAt { get; }
        IDictionary<string, object> Metadata { get; }
        IReadOnlyCollection<IExecutionStep> Steps { get; }
        CancellationToken CancellationToken { get; }

        /// <summary>AI prompt inputs: system/user prompts, RAG context, memory, conversation history.</summary>
        PromptContext? Prompt { get; }
    }
}