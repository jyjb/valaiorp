namespace Valaiorp.Memory.Implementations
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Memory.Models;

    /// <summary>
    /// File-backed long-term memory. Each context, log stream, and feedback stream is written to
    /// its own file under the configured directory so data survives process restarts.
    /// </summary>
    public sealed class JsonlLongTermMemory : ILongTermMemory
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly string _contextsDir;
        private readonly string _logsDir;
        private readonly string _feedbackDir;

        // Per-contextId semaphores so contexts never block each other.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public JsonlLongTermMemory(string directory)
        {
            _contextsDir = Path.Combine(directory, "contexts");
            _logsDir     = Path.Combine(directory, "logs");
            _feedbackDir = Path.Combine(directory, "feedback");
            Directory.CreateDirectory(_contextsDir);
            Directory.CreateDirectory(_logsDir);
            Directory.CreateDirectory(_feedbackDir);
        }

        // ── IExecutionContext ──────────────────────────────────────────────────────

        public async Task StoreAsync(string contextId, IExecutionContext context, CancellationToken ct = default)
        {
            var dto = new StoredContext
            {
                Id        = context.Id,
                SessionId = context.SessionId,
                UserId    = context.UserId,
                CreatedAt = context.CreatedAt,
                ExpiresAt = context.ExpiresAt,
                Metadata  = new Dictionary<string, object>(context.Metadata),
                Prompt    = context.Prompt
            };
            var path = ContextPath(contextId);
            var sem  = GetLock(contextId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, _opts), ct).ConfigureAwait(false); }
            finally { sem.Release(); }
        }

        public async Task<IExecutionContext?> RetrieveAsync(string contextId, CancellationToken ct = default)
        {
            var path = ContextPath(contextId);
            if (!File.Exists(path)) return null;
            var sem = GetLock(contextId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                var dto  = JsonSerializer.Deserialize<StoredContext>(json, _opts);
                if (dto == null) return null;
                return new ExecutionContext
                {
                    Id        = dto.Id,
                    SessionId = dto.SessionId,
                    UserId    = dto.UserId,
                    ExpiresAt = dto.ExpiresAt,
                    Metadata  = dto.Metadata ?? [],
                    Prompt    = dto.Prompt
                };
            }
            finally { sem.Release(); }
        }

        // ── ExecutionLog ──────────────────────────────────────────────────────────

        public async Task StoreLogAsync(ExecutionLog log, CancellationToken ct = default)
        {
            var path = Path.Combine(_logsDir, $"{log.ContextId}.jsonl");
            var sem  = GetLock(log.ContextId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try { await AppendLineAsync(path, JsonSerializer.Serialize(log, _opts), ct).ConfigureAwait(false); }
            finally { sem.Release(); }
        }

        public async Task<IReadOnlyList<ExecutionLog>> RetrieveLogsAsync(string contextId, CancellationToken ct = default)
        {
            var path = Path.Combine(_logsDir, $"{contextId}.jsonl");
            return await ReadJsonlAsync<ExecutionLog>(path, contextId, ct).ConfigureAwait(false);
        }

        // ── FeedbackEntry ─────────────────────────────────────────────────────────

        public async Task StoreFeedbackAsync(FeedbackEntry feedback, CancellationToken ct = default)
        {
            var path = Path.Combine(_feedbackDir, $"{feedback.ContextId}.jsonl");
            var sem  = GetLock(feedback.ContextId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try { await AppendLineAsync(path, JsonSerializer.Serialize(feedback, _opts), ct).ConfigureAwait(false); }
            finally { sem.Release(); }
        }

        public async Task<IReadOnlyList<FeedbackEntry>> RetrieveFeedbackAsync(string contextId, CancellationToken ct = default)
        {
            var path = Path.Combine(_feedbackDir, $"{contextId}.jsonl");
            return await ReadJsonlAsync<FeedbackEntry>(path, contextId, ct).ConfigureAwait(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string ContextPath(string contextId)
            => Path.Combine(_contextsDir, $"{contextId}.json");

        private SemaphoreSlim GetLock(string key)
            => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        private static Task AppendLineAsync(string path, string line, CancellationToken ct)
            => File.AppendAllTextAsync(path, line + Environment.NewLine, ct);

        private async Task<IReadOnlyList<T>> ReadJsonlAsync<T>(string path, string contextId, CancellationToken ct)
        {
            if (!File.Exists(path)) return [];
            var sem = GetLock(contextId);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
                var result = new List<T>(lines.Length);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var item = JsonSerializer.Deserialize<T>(line, _opts);
                    if (item != null) result.Add(item);
                }
                return result;
            }
            finally { sem.Release(); }
        }

        // ── DTO ───────────────────────────────────────────────────────────────────

        private sealed class StoredContext
        {
            public string Id { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset? ExpiresAt { get; set; }
            public Dictionary<string, object>? Metadata { get; set; }
            public PromptContext? Prompt { get; set; }
        }
    }
}
