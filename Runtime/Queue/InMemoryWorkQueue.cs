namespace Valaiorp.Runtime.Queue
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    /// <summary>
    /// In-memory queue. Safe for single-process use (dev/test/single-bot).
    /// For multi-machine bots replace with SqlWorkQueue or a broker-backed implementation.
    /// </summary>
    public sealed class InMemoryWorkQueue : IWorkQueue
    {
        private readonly ConcurrentDictionary<string, WorkItem>   _items    = new();
        private readonly ConcurrentDictionary<string, QueueRun>   _runs     = new();
        private readonly SemaphoreSlim                             _lock     = new(1, 1);

        // ── Run lifecycle ─────────────────────────────────────────────────────────

        public Task<QueueRun> StartRunAsync(string queueId, string botId, CancellationToken ct = default)
        {
            var run = new QueueRun { QueueId = queueId, BotId = botId };
            _runs[run.RunId] = run;
            return Task.FromResult(run);
        }

        public Task EndRunAsync(string runId, CancellationToken ct = default)
        {
            if (_runs.TryGetValue(runId, out var run))
                run.EndedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        // ── Population ────────────────────────────────────────────────────────────

        public async Task PopulateAsync(string queueId, IEnumerable<IWorkItem> items, CancellationToken ct = default)
        {
            foreach (var item in items)
                await EnqueueAsync(item, ct).ConfigureAwait(false);
        }

        public Task EnqueueAsync(IWorkItem item, CancellationToken ct = default)
        {
            var wi = item as WorkItem ?? new WorkItem
            {
                QueueId   = item.QueueId,
                Reference = item.Reference,
                Tag       = item.Tag,
                Priority  = item.Priority,
                Payload   = item.Payload
            };
            // Idempotent by Reference
            if (wi.Reference != null &&
                _items.Values.Any(x => x.QueueId == wi.QueueId && x.Reference == wi.Reference))
                return Task.CompletedTask;

            _items[wi.ItemId] = wi;
            return Task.CompletedTask;
        }

        // ── Assignment ────────────────────────────────────────────────────────────

        public Task AssignQueueToBotAsync(string queueId, string botId, CancellationToken ct = default)
        {
            foreach (var item in _items.Values
                .Where(i => i.QueueId == queueId && i.Status == WorkItemStatus.Pending && i.AssignedToBotId == null))
                item.AssignedToBotId = botId;
            return Task.CompletedTask;
        }

        // ── Retrieval ─────────────────────────────────────────────────────────────

        public async Task<IWorkItem?> GetNextItemAsync(
            string queueId, string? botId = null, string? tag = null,
            string? reference = null, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var candidate = _items.Values
                    .Where(i => i.QueueId == queueId
                                && i.Status == WorkItemStatus.Pending
                                && (i.ScheduledAt == null || i.ScheduledAt <= DateTimeOffset.UtcNow)
                                && (botId    == null || i.AssignedToBotId == null || i.AssignedToBotId == botId)
                                && (tag      == null || i.Tag       == tag)
                                && (reference == null || i.Reference == reference))
                    .OrderByDescending(i => i.Priority)
                    .ThenBy(i => i.EnqueuedAt)
                    .FirstOrDefault();

                if (candidate == null) return null;

                candidate.Status     = WorkItemStatus.InProgress;
                candidate.StartedAt  = DateTimeOffset.UtcNow;
                candidate.AttemptCount++;
                if (botId != null) candidate.AssignedToBotId = botId;
                return candidate;
            }
            finally { _lock.Release(); }
        }

        // ── Status updates ────────────────────────────────────────────────────────

        public Task MarkCompletedAsync(string itemId, IDictionary<string, object>? output = null, CancellationToken ct = default)
        {
            if (_items.TryGetValue(itemId, out var item))
            {
                item.Status      = WorkItemStatus.Completed;
                item.CompletedAt = DateTimeOffset.UtcNow;
                item.Output      = output;
            }
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string itemId, string reason, string? exceptionType = null,
            string? exceptionDetail = null, int maxAttempts = 3, CancellationToken ct = default)
        {
            if (!_items.TryGetValue(itemId, out var item)) return Task.CompletedTask;

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
                item.Status     = WorkItemStatus.Pending;
                item.StartedAt  = null;
                item.CompletedAt = null;
            }
            return Task.CompletedTask;
        }

        // ── Reporting ─────────────────────────────────────────────────────────────

        public Task<QueueReport> GetReportAsync(string queueId, CancellationToken ct = default)
        {
            var all   = _items.Values.Where(i => i.QueueId == queueId).ToList();
            var runs  = _runs.Values.Where(r => r.QueueId == queueId).ToList();
            var failed = all.Where(i => i.Status is WorkItemStatus.Failed or WorkItemStatus.DeadLetter).ToList<IWorkItem>();

            var completedWithTime = all
                .Where(i => i.Status == WorkItemStatus.Completed && i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Select(i => (i.CompletedAt!.Value - i.StartedAt!.Value))
                .ToList();

            var avg = completedWithTime.Count > 0
                ? TimeSpan.FromTicks((long)completedWithTime.Average(t => t.Ticks))
                : TimeSpan.Zero;

            var firstRun = runs.MinBy(r => r.StartedAt);
            var lastEnd  = runs.Where(r => r.EndedAt.HasValue).MaxBy(r => r.EndedAt);
            TimeSpan? elapsed = firstRun != null && lastEnd?.EndedAt != null
                ? lastEnd.EndedAt.Value - firstRun.StartedAt
                : null;

            var report = new QueueReport
            {
                QueueId              = queueId,
                TotalItems           = all.Count,
                Pending              = all.Count(i => i.Status == WorkItemStatus.Pending),
                InProgress           = all.Count(i => i.Status == WorkItemStatus.InProgress),
                Completed            = all.Count(i => i.Status == WorkItemStatus.Completed),
                Failed               = all.Count(i => i.Status == WorkItemStatus.Failed),
                DeadLetter           = all.Count(i => i.Status == WorkItemStatus.DeadLetter),
                AverageProcessingTime = avg,
                TotalElapsedTime     = elapsed,
                Runs                 = runs,
                FailedItems          = failed,
                AllItems             = all.ToList<IWorkItem>()
            };
            return Task.FromResult(report);
        }

        public Task<int> GetPendingCountAsync(string queueId, CancellationToken ct = default)
            => Task.FromResult(_items.Values.Count(i => i.QueueId == queueId && i.Status == WorkItemStatus.Pending));

        public Task<int> GetInProgressCountAsync(string queueId, CancellationToken ct = default)
            => Task.FromResult(_items.Values.Count(i => i.QueueId == queueId && i.Status == WorkItemStatus.InProgress));

        public Task<int> GetDeadLetterCountAsync(string queueId, CancellationToken ct = default)
            => Task.FromResult(_items.Values.Count(i => i.QueueId == queueId && i.Status == WorkItemStatus.DeadLetter));
    }
}
