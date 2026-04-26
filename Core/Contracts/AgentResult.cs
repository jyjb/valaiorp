namespace Valaiorp.Core.Contracts
{
    public sealed class AgentResult
    {
        public string AgentId { get; init; } = string.Empty;
        public string ConversationId { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? Output { get; init; }
        public string? Error { get; init; }

        /// <summary>Messages this agent wants the orchestrator to dispatch to other agents.</summary>
        public IReadOnlyList<AgentMessage> DelegatedMessages { get; init; } = [];

        public IReadOnlyDictionary<string, object> Metadata { get; init; }
            = new Dictionary<string, object>();

        public static AgentResult Success(string agentId, string conversationId, string output,
            IReadOnlyList<AgentMessage>? delegated = null)
            => new() { AgentId = agentId, ConversationId = conversationId,
                       IsSuccess = true, Output = output,
                       DelegatedMessages = delegated ?? [] };

        public static AgentResult Failure(string agentId, string conversationId, string error)
            => new() { AgentId = agentId, ConversationId = conversationId,
                       IsSuccess = false, Error = error };
    }
}
