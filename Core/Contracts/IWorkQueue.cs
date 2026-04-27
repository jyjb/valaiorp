namespace Valaiorp.Core.Contracts
{
    /// <summary>
    /// Enterprise work queue abstraction. Implementations:
    ///   InMemoryWorkQueue  — single-process, dev/test
    ///   JsonlWorkQueue     — JSONL file, single local bot
    ///   SqlWorkQueue       — SQL table, single or multi-bot, multi-machine
    ///
    /// All bots sharing the same IWorkQueue instance (or same SQL table) form a
    /// bot pool — they compete for items from the same queue automatically.
    /// </summary>
    public interface IWorkQueue
    {
        // ── Run lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a bot session start against a queue.
        /// Returns the RunId that must be passed to EndRunAsync.
        /// </summary>
        Task<QueueRun> StartRunAsync(string queueId, string botId, CancellationToken ct = default);

        /// <summary>Marks a bot session as ended and records the timestamp.</summary>
        Task EndRunAsync(string runId, CancellationToken ct = default);

        // ── Population ────────────────────────────────────────────────────────────

        /// <summary>
        /// Bulk-adds work items to a queue. Items must have QueueId set.
        /// Existing items with the same Reference are skipped (idempotent).
        /// </summary>
        Task PopulateAsync(string queueId, IEnumerable<IWorkItem> items, CancellationToken ct = default);

        /// <summary>Adds a single work item to a queue.</summary>
        Task EnqueueAsync(IWorkItem item, CancellationToken ct = default);

        // ── Assignment ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-assigns all pending items in a queue to a specific bot.
        /// Items already assigned to another bot are not touched.
        /// Use when you want a dedicated bot per queue rather than free competition.
        /// </summary>
        Task AssignQueueToBotAsync(string queueId, string botId, CancellationToken ct = default);

        // ── Retrieval ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches and locks the next available item.
        /// Filters: queueId (required), tag, reference, botId (assigned-to).
        /// Returns null when no matching item is available.
        /// </summary>
        Task<IWorkItem?> GetNextItemAsync(
            string  queueId,
            string? botId     = null,
            string? tag       = null,
            string? reference = null,
            CancellationToken ct = default);

        // ── Status updates ────────────────────────────────────────────────────────

        /// <summary>Marks a work item as successfully completed.</summary>
        Task MarkCompletedAsync(
            string                        itemId,
            IDictionary<string, object>?  output = null,
            CancellationToken             ct     = default);

        /// <summary>
        /// Marks a work item as failed.
        /// If AttemptCount &lt; maxAttempts the item is re-queued for retry.
        /// If AttemptCount >= maxAttempts it is moved to DeadLetter.
        /// </summary>
        Task MarkFailedAsync(
            string  itemId,
            string  reason,
            string? exceptionType   = null,
            string? exceptionDetail = null,
            int     maxAttempts     = 3,
            CancellationToken ct    = default);

        // ── Reporting ─────────────────────────────────────────────────────────────

        /// <summary>Returns a full status and statistics report for a queue.</summary>
        Task<QueueReport> GetReportAsync(string queueId, CancellationToken ct = default);

        // ── Counts (lightweight) ──────────────────────────────────────────────────

        Task<int> GetPendingCountAsync(string queueId, CancellationToken ct = default);
        Task<int> GetInProgressCountAsync(string queueId, CancellationToken ct = default);
        Task<int> GetDeadLetterCountAsync(string queueId, CancellationToken ct = default);
    }
}
