namespace Valaiorp.Memory.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Cross-agent conversation memory keyed by ConversationId.
    /// Shared across all agents participating in the same conversation turn.
    /// </summary>
    public interface IConversationMemory
    {
        Task AddTurnAsync(string conversationId, ConversationTurn turn, CancellationToken ct = default);

        Task<IReadOnlyList<ConversationTurn>> GetHistoryAsync(string conversationId, CancellationToken ct = default);

        /// <summary>Returns the last <paramref name="maxTurns"/> turns for context window management.</summary>
        Task<IReadOnlyList<ConversationTurn>> GetRecentHistoryAsync(string conversationId, int maxTurns, CancellationToken ct = default);

        Task ClearAsync(string conversationId, CancellationToken ct = default);

        Task<bool> ExistsAsync(string conversationId, CancellationToken ct = default);
    }
}
