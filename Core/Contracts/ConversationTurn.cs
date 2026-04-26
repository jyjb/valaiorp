namespace Valaiorp.Core.Contracts
{
    public sealed class ConversationTurn
    {
        public string Role { get; set; } = string.Empty;   // "user" | "assistant" | "system"
        public string Content { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}
