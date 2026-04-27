namespace Valaiorp.Runtime.Queue
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    /// <summary>
    /// JSONL file-backed queue. Suitable for single local bot, no external DB needed.
    /// Each work item is stored as one JSON line in the file. State is persisted on
    /// every status change so execution survives process restarts.
    ///
    /// File path convention:
    ///   {directory}/{queueId}.queue.jsonl   — work items
    ///   {directory}/{queueId}.runs.jsonl    — run records
    /// </summary>
    public sealed class JsonlWorkQueue : IWorkQueue
    {
        private readonly string       _directory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
            WriteIndented               = false,
            DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public JsonlWorkQueue(string directory = "queues")
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
        }

        // ── Run lifecycle ─────────────────────────────────────────────────────────

        public async Task<QueueRun> StartRunAsync(string queueId, string botId, CancellationToken ct = default)
        {
            var run = new QueueRun { QueueId = queueId, BotId = botId };
            await AppendRunAsync(queueId, run, ct).ConfigureAwait(false);
            return run;
        }

        public async Task EndRunAsync(string runId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Find the queue this run belongs to by scanning all run files
                foreach (var runFile in Directory.GetFiles(_directory, "*.runs.jsonl"))
                {
                    var runs = await ReadAllRunsFromFileAsync(runFile, ct).ConfigureAwait(false);
                    var target = runs.FirstOrDefault(r => r.RunId == runId);
                    if (target == null) continue;
                    target.EndedAt = DateTimeOffset.UtcNow;
                    await WriteAllRunsToFileAsync(runFile, runs, ct).ConfigureAwait(false);
                    return;
                }
            }
            finally { _lock.Release(); }
        }

        // ── Population ────────────────────────────────────────────────────────────

        public async Task PopulateAsync(string queueId, IEnumerable<IWorkItem> items, CancellationToken ct = default)
        {
            foreach (var item in items)
                await EnqueueAsync(item, ct).ConfigureAwait(false);
        }

        public async Task EnqueueAsync(IWorkItem item, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var all = await ReadAllItemsAsync(item.QueueId, ct).ConfigureAwait(false);

                // Idempotent by Reference
                if (item.Reference != null && all.Any(x => x.Reference == item.Reference))
                    return;

                var wi = ToWorkItem(item);
                all.Add(wi);
                await WriteAllItemsAsync(item.QueueId, all, ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        // ── Assignment ────────────────────────────────────────────────────────────

        public async Task AssignQueueToBotAsync(string queueId, string botId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var all = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
                foreach (var i in all.Where(x => x.Status == WorkItemStatus.Pending && x.AssignedToBotId == null))
                    i.AssignedToBotId = botId;
                await WriteAllItemsAsync(queueId, all, ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        // ── Retrieval ─────────────────────────────────────────────────────────────

        public async Task<IWorkItem?> GetNextItemAsync(
            string queueId, string? botId = null, string? tag = null,
            string? reference = null, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var all = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);

                var candidate = all
                    .Where(i => i.Status == WorkItemStatus.Pending
                                && (i.ScheduledAt == null || i.ScheduledAt <= DateTimeOffset.UtcNow)
                                && (botId     == null || i.AssignedToBotId == null || i.AssignedToBotId == botId)
                                && (tag       == null || i.Tag       == tag)
                                && (reference == null || i.Reference == reference))
                    .OrderByDescending(i => i.Priority)
                    .ThenBy(i => i.EnqueuedAt)
                    .FirstOrDefault();

                if (candidate == null) return null;

                candidate.Status       = WorkItemStatus.InProgress;
                candidate.StartedAt    = DateTimeOffset.UtcNow;
                candidate.AttemptCount++;
                if (botId != null) candidate.AssignedToBotId = botId;

                await WriteAllItemsAsync(queueId, all, ct).ConfigureAwait(false);
                return candidate;
            }
            finally { _lock.Release(); }
        }

        // ── Status updates ────────────────────────────────────────────────────────

        public async Task MarkCompletedAsync(string itemId, IDictionary<string, object>? output = null, CancellationToken ct = default)
            => await UpdateItemAsync(itemId, ct, item =>
            {
                item.Status      = WorkItemStatus.Completed;
                item.CompletedAt = DateTimeOffset.UtcNow;
                item.Output      = output;
            }).ConfigureAwait(false);

        public async Task MarkFailedAsync(string itemId, string reason, string? exceptionType = null,
            string? exceptionDetail = null, int maxAttempts = 3, CancellationToken ct = default)
            => await UpdateItemAsync(itemId, ct, item =>
            {
                item.FailureReason   = reason;
                item.ExceptionType   = exceptionType;
                item.ExceptionDetail = exceptionDetail;
                item.CompletedAt     = DateTimeOffset.UtcNow;

                if (item.AttemptCount >= maxAttempts)
                {
                    item.Status = WorkItemStatus.DeadLetter;
                }
                else
                {
                    item.Status      = WorkItemStatus.Pending;
                    item.StartedAt   = null;
                    item.CompletedAt = null;
                }
            }).ConfigureAwait(false);

        // ── Reporting ─────────────────────────────────────────────────────────────

        public async Task<QueueReport> GetReportAsync(string queueId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var all  = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
                var runs = await ReadAllRunsAsync(queueId, ct).ConfigureAwait(false);
                return BuildReport(queueId, all, runs);
            }
            finally { _lock.Release(); }
        }

        public async Task<int> GetPendingCountAsync(string queueId, CancellationToken ct = default)
        {
            var all = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
            return all.Count(i => i.Status == WorkItemStatus.Pending);
        }

        public async Task<int> GetInProgressCountAsync(string queueId, CancellationToken ct = default)
        {
            var all = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
            return all.Count(i => i.Status == WorkItemStatus.InProgress);
        }

        public async Task<int> GetDeadLetterCountAsync(string queueId, CancellationToken ct = default)
        {
            var all = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
            return all.Count(i => i.Status == WorkItemStatus.DeadLetter);
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private string ItemsFile(string queueId) => Path.Combine(_directory, $"{queueId}.queue.jsonl");
        private string RunsFile(string queueId)  => Path.Combine(_directory, $"{queueId}.runs.jsonl");

        private async Task<List<WorkItem>> ReadAllItemsAsync(string queueId, CancellationToken ct)
        {
            var file = ItemsFile(queueId);
            if (!File.Exists(file)) return new List<WorkItem>();
            var lines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
            return lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<WorkItem>(l, _json)!)
                .Where(i => i != null)
                .ToList();
        }

        private async Task WriteAllItemsAsync(string queueId, List<WorkItem> items, CancellationToken ct)
        {
            var lines = items.Select(i => JsonSerializer.Serialize(i, _json));
            await File.WriteAllLinesAsync(ItemsFile(queueId), lines, ct).ConfigureAwait(false);
        }

        private async Task<List<QueueRun>> ReadAllRunsAsync(string queueId, CancellationToken ct)
            => await ReadAllRunsFromFileAsync(RunsFile(queueId), ct).ConfigureAwait(false);

        private static async Task<List<QueueRun>> ReadAllRunsFromFileAsync(string file, CancellationToken ct)
        {
            if (!File.Exists(file)) return new List<QueueRun>();
            var lines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
            return lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<QueueRun>(l, _json)!)
                .Where(r => r != null)
                .ToList();
        }

        private async Task AppendRunAsync(string queueId, QueueRun run, CancellationToken ct)
        {
            await File.AppendAllTextAsync(RunsFile(queueId),
                JsonSerializer.Serialize(run, _json) + Environment.NewLine, ct).ConfigureAwait(false);
        }

        private static async Task WriteAllRunsToFileAsync(string file, List<QueueRun> runs, CancellationToken ct)
        {
            var lines = runs.Select(r => JsonSerializer.Serialize(r, _json));
            await File.WriteAllLinesAsync(file, lines, ct).ConfigureAwait(false);
        }

        private async Task UpdateItemAsync(string itemId, CancellationToken ct, Action<WorkItem> mutate)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Scan all queue files to find the item
                foreach (var file in Directory.GetFiles(_directory, "*.queue.jsonl"))
                {
                    var queueId = Path.GetFileName(file).Replace(".queue.jsonl", "");
                    var all     = await ReadAllItemsAsync(queueId, ct).ConfigureAwait(false);
                    var target  = all.FirstOrDefault(i => i.ItemId == itemId);
                    if (target == null) continue;
                    mutate(target);
                    await WriteAllItemsAsync(queueId, all, ct).ConfigureAwait(false);
                    return;
                }
            }
            finally { _lock.Release(); }
        }

        private static WorkItem ToWorkItem(IWorkItem src) => src as WorkItem ?? new WorkItem
        {
            QueueId   = src.QueueId,
            Reference = src.Reference,
            Tag       = src.Tag,
            Priority  = src.Priority,
            Payload   = src.Payload
        };

        private static QueueReport BuildReport(string queueId, List<WorkItem> all, List<QueueRun> runs)
        {
            var failed = all.Where(i => i.Status is WorkItemStatus.Failed or WorkItemStatus.DeadLetter).ToList<IWorkItem>();
            var completedTimes = all
                .Where(i => i.Status == WorkItemStatus.Completed && i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Select(i => i.CompletedAt!.Value - i.StartedAt!.Value)
                .ToList();

            var avg     = completedTimes.Count > 0 ? TimeSpan.FromTicks((long)completedTimes.Average(t => t.Ticks)) : TimeSpan.Zero;
            var firstRun = runs.MinBy(r => r.StartedAt);
            var lastEnd  = runs.Where(r => r.EndedAt.HasValue).MaxBy(r => r.EndedAt);
            TimeSpan? elapsed = firstRun != null && lastEnd?.EndedAt != null ? lastEnd.EndedAt.Value - firstRun.StartedAt : null;

            return new QueueReport
            {
                QueueId               = queueId,
                TotalItems            = all.Count,
                Pending               = all.Count(i => i.Status == WorkItemStatus.Pending),
                InProgress            = all.Count(i => i.Status == WorkItemStatus.InProgress),
                Completed             = all.Count(i => i.Status == WorkItemStatus.Completed),
                Failed                = all.Count(i => i.Status == WorkItemStatus.Failed),
                DeadLetter            = all.Count(i => i.Status == WorkItemStatus.DeadLetter),
                AverageProcessingTime = avg,
                TotalElapsedTime      = elapsed,
                Runs                  = runs,
                FailedItems           = failed,
                AllItems              = all.ToList<IWorkItem>()
            };
        }
    }
}
