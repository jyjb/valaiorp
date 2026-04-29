namespace Valaiorp.Memory.Implementations
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;

    /// <summary>
    /// File-backed conversation memory. Each conversation is appended to its own JSONL file
    /// so history survives process restarts.
    /// </summary>
    public sealed class JsonlConversationMemory : IConversationMemory
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _directory;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public JsonlConversationMemory(string directory)
        {
            _directory = Path.Combine(directory, "conversations");
            Directory.CreateDirectory(_directory);
        }

        public async Task AddTurnAsync(string conversationId, ConversationTurn turn, CancellationToken ct = default)
        {
            var sem = GetLock(conversationId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(
                    FilePath(conversationId),
                    JsonSerializer.Serialize(turn, _opts) + Environment.NewLine,
                    ct).ConfigureAwait(false);
            }
            finally { sem.Release(); }
        }

        public async Task<IReadOnlyList<ConversationTurn>> GetHistoryAsync(string conversationId, CancellationToken ct = default)
            => await ReadAllTurnsAsync(conversationId, ct).ConfigureAwait(false);

        public async Task<IReadOnlyList<ConversationTurn>> GetRecentHistoryAsync(
            string conversationId, int maxTurns, CancellationToken ct = default)
        {
            var all = await ReadAllTurnsAsync(conversationId, ct).ConfigureAwait(false);
            return all.Count <= maxTurns ? all : all.Skip(all.Count - maxTurns).ToList();
        }

        public async Task ClearAsync(string conversationId, CancellationToken ct = default)
        {
            var path = FilePath(conversationId);
            if (!File.Exists(path)) return;
            var sem = GetLock(conversationId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try { File.Delete(path); }
            finally { sem.Release(); }
        }

        public Task<bool> ExistsAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult(File.Exists(FilePath(conversationId)));

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string FilePath(string conversationId)
            => Path.Combine(_directory, $"{conversationId}.jsonl");

        private SemaphoreSlim GetLock(string key)
            => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        private async Task<IReadOnlyList<ConversationTurn>> ReadAllTurnsAsync(string conversationId, CancellationToken ct)
        {
            var path = FilePath(conversationId);
            if (!File.Exists(path)) return [];
            var sem = GetLock(conversationId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
                var result = new List<ConversationTurn>(lines.Length);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var turn = JsonSerializer.Deserialize<ConversationTurn>(line, _opts);
                    if (turn != null) result.Add(turn);
                }
                return result;
            }
            finally { sem.Release(); }
        }
    }
}
