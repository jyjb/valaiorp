namespace Valaiorp.Core.Contracts
{
    public sealed class AgentMessage
    {
        public string MessageId { get; } = Guid.NewGuid().ToString("N");
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Sender agent ID. Null when the message originates from the user / system.</summary>
        public string? FromAgentId { get; set; }

        /// <summary>Target agent ID. Null routes to the default orchestrator.</summary>
        public string? ToAgentId { get; set; }

        public PromptContext Prompt { get; set; } = new();

        /// <summary>Arbitrary data passed between agents (tool results, structured outputs).</summary>
        public IReadOnlyDictionary<string, object> Payload { get; set; }
            = new Dictionary<string, object>();

        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    }
}
