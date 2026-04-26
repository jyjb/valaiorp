namespace Valaiorp.Memory.Implementations
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;

    public sealed class InMemoryConversationMemory : IConversationMemory
    {
        private readonly ConcurrentDictionary<string, List<ConversationTurn>> _store = new();

        public Task AddTurnAsync(string conversationId, ConversationTurn turn, CancellationToken ct = default)
        {
            var turns = _store.GetOrAdd(conversationId, _ => []);
            lock (turns)
            {
                turns.Add(turn);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationTurn>> GetHistoryAsync(string conversationId, CancellationToken ct = default)
        {
            if (_store.TryGetValue(conversationId, out var turns))
            {
                lock (turns)
                {
                    return Task.FromResult<IReadOnlyList<ConversationTurn>>([.. turns]);
                }
            }
            return Task.FromResult<IReadOnlyList<ConversationTurn>>([]);
        }

        public Task<IReadOnlyList<ConversationTurn>> GetRecentHistoryAsync(string conversationId, int maxTurns, CancellationToken ct = default)
        {
            if (_store.TryGetValue(conversationId, out var turns))
            {
                lock (turns)
                {
                    var recent = turns.Count <= maxTurns
                        ? [.. turns]
                        : turns.GetRange(turns.Count - maxTurns, maxTurns).ToArray();
                    return Task.FromResult<IReadOnlyList<ConversationTurn>>(recent);
                }
            }
            return Task.FromResult<IReadOnlyList<ConversationTurn>>([]);
        }

        public Task ClearAsync(string conversationId, CancellationToken ct = default)
        {
            _store.TryRemove(conversationId, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult(_store.ContainsKey(conversationId));
    }
}
