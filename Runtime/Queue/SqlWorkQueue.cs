namespace Valaiorp.Runtime.Queue
{
    using System.Data;
    using System.Data.Common;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    /// <summary>
    /// SQL-backed work queue. Works with any ADO.NET-compatible provider:
    /// SQL Server, PostgreSQL, SQLite, MySQL, Oracle.
    ///
    /// Subclass and override CreateConnection() to provide your connection.
    /// The SQL schema is standard ANSI SQL — see CreateSchemaAsync() for DDL.
    ///
    /// Multi-machine safe: GetNextItemAsync uses a SELECT FOR UPDATE / UPDATE with
    /// optimistic locking so multiple bots never claim the same item.
    ///
    /// Usage (SQL Server example):
    ///   public sealed class MySqlQueue : SqlWorkQueue
    ///   {
    ///       protected override DbConnection CreateConnection()
    ///           => new SqlConnection("Server=prod;Database=BotQueue;...");
    ///   }
    /// </summary>
    public abstract class SqlWorkQueue : IWorkQueue
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented        = false
        };

        // ── Schema DDL ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the required tables if they do not exist.
        /// Call once at application startup.
        /// </summary>
        public async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            const string ddl = """
                CREATE TABLE IF NOT EXISTS queue_runs (
                    run_id       VARCHAR(100)   PRIMARY KEY,
                    queue_id     VARCHAR(100)   NOT NULL,
                    bot_id       VARCHAR(100)   NOT NULL,
                    machine_name VARCHAR(200),
                    started_at   TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ended_at     TIMESTAMP      NULL
                );

                CREATE TABLE IF NOT EXISTS work_items (
                    item_id            VARCHAR(100)   PRIMARY KEY,
                    queue_id           VARCHAR(100)   NOT NULL,
                    reference          VARCHAR(500)   NULL,
                    tag                VARCHAR(200)   NULL,
                    priority           INTEGER        NOT NULL DEFAULT 0,
                    attempt_count      INTEGER        NOT NULL DEFAULT 0,
                    status             VARCHAR(50)    NOT NULL DEFAULT 'Pending',
                    assigned_to_bot_id VARCHAR(100)   NULL,
                    enqueued_at        TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    scheduled_at       TIMESTAMP      NULL,
                    started_at         TIMESTAMP      NULL,
                    completed_at       TIMESTAMP      NULL,
                    failure_reason     TEXT           NULL,
                    exception_type     VARCHAR(500)   NULL,
                    exception_detail   TEXT           NULL,
                    payload            TEXT           NULL,
                    output             TEXT           NULL
                );

                CREATE INDEX IF NOT EXISTS idx_work_items_queue_status
                    ON work_items(queue_id, status, priority DESC, enqueued_at ASC);

                CREATE UNIQUE INDEX IF NOT EXISTS idx_work_items_ref
                    ON work_items(queue_id, reference)
                    WHERE reference IS NOT NULL;
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Abstract factory ──────────────────────────────────────────────────────

        protected abstract DbConnection CreateConnection();

        // ── Run lifecycle ─────────────────────────────────────────────────────────

        public async Task<QueueRun> StartRunAsync(string queueId, string botId, CancellationToken ct = default)
        {
            var run = new QueueRun { QueueId = queueId, BotId = botId };
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO queue_runs (run_id, queue_id, bot_id, machine_name, started_at)
                VALUES (@runId, @queueId, @botId, @machine, @startedAt)
                """;
            AddParam(cmd, "@runId",     run.RunId);
            AddParam(cmd, "@queueId",   run.QueueId);
            AddParam(cmd, "@botId",     run.BotId);
            AddParam(cmd, "@machine",   run.MachineName);
            AddParam(cmd, "@startedAt", run.StartedAt);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return run;
        }

        public async Task EndRunAsync(string runId, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE queue_runs SET ended_at = @endedAt WHERE run_id = @runId";
            AddParam(cmd, "@endedAt", DateTimeOffset.UtcNow);
            AddParam(cmd, "@runId",   runId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Population ────────────────────────────────────────────────────────────

        public async Task PopulateAsync(string queueId, IEnumerable<IWorkItem> items, CancellationToken ct = default)
        {
            foreach (var item in items)
                await EnqueueAsync(item, ct).ConfigureAwait(false);
        }

        public async Task EnqueueAsync(IWorkItem item, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            // INSERT IGNORE / ON CONFLICT DO NOTHING for idempotency by reference
            cmd.CommandText = """
                INSERT INTO work_items
                    (item_id, queue_id, reference, tag, priority, status, enqueued_at, scheduled_at, payload)
                VALUES
                    (@itemId, @queueId, @reference, @tag, @priority, 'Pending', @enqueuedAt, @scheduledAt, @payload)
                ON CONFLICT (queue_id, reference) DO NOTHING
                """;
            AddParam(cmd, "@itemId",      item.ItemId);
            AddParam(cmd, "@queueId",     item.QueueId);
            AddParam(cmd, "@reference",   (object?)item.Reference   ?? DBNull.Value);
            AddParam(cmd, "@tag",         (object?)item.Tag         ?? DBNull.Value);
            AddParam(cmd, "@priority",    item.Priority);
            AddParam(cmd, "@enqueuedAt",  item.EnqueuedAt);
            AddParam(cmd, "@scheduledAt", (object?)item.ScheduledAt ?? DBNull.Value);
            AddParam(cmd, "@payload",     JsonSerializer.Serialize(item.Payload, _json));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Assignment ────────────────────────────────────────────────────────────

        public async Task AssignQueueToBotAsync(string queueId, string botId, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE work_items
                SET assigned_to_bot_id = @botId
                WHERE queue_id = @queueId
                  AND status = 'Pending'
                  AND assigned_to_bot_id IS NULL
                """;
            AddParam(cmd, "@botId",   botId);
            AddParam(cmd, "@queueId", queueId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Retrieval ─────────────────────────────────────────────────────────────

        public async Task<IWorkItem?> GetNextItemAsync(
            string queueId, string? botId = null, string? tag = null,
            string? reference = null, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            // Atomic claim: find + update in one statement to prevent race conditions
            // across multiple bots on multiple machines.
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                UPDATE work_items
                SET status        = 'InProgress',
                    started_at    = @now,
                    attempt_count = attempt_count + 1
                    {(botId != null ? ", assigned_to_bot_id = @botId" : "")}
                WHERE item_id = (
                    SELECT item_id FROM work_items
                    WHERE queue_id  = @queueId
                      AND status    = 'Pending'
                      {(botId    != null ? "AND (assigned_to_bot_id IS NULL OR assigned_to_bot_id = @botId)" : "")}
                      {(tag      != null ? "AND tag       = @tag"       : "")}
                      {(reference != null ? "AND reference = @reference" : "")}
                      AND (scheduled_at IS NULL OR scheduled_at <= @now)
                    ORDER BY priority DESC, enqueued_at ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *
                """;

            AddParam(cmd, "@queueId", queueId);
            AddParam(cmd, "@now",     DateTimeOffset.UtcNow);
            if (botId    != null) AddParam(cmd, "@botId",     botId);
            if (tag      != null) AddParam(cmd, "@tag",       tag);
            if (reference != null) AddParam(cmd, "@reference", reference);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            return ReadWorkItem(reader);
        }

        // ── Status updates ────────────────────────────────────────────────────────

        public async Task MarkCompletedAsync(string itemId, IDictionary<string, object>? output = null, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE work_items
                SET status = 'Completed', completed_at = @now, output = @output
                WHERE item_id = @itemId
                """;
            AddParam(cmd, "@itemId", itemId);
            AddParam(cmd, "@now",    DateTimeOffset.UtcNow);
            AddParam(cmd, "@output", output != null ? (object)JsonSerializer.Serialize(output, _json) : DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        public async Task MarkFailedAsync(string itemId, string reason, string? exceptionType = null,
            string? exceptionDetail = null, int maxAttempts = 3, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();

            // One SQL statement — DB decides Pending vs DeadLetter based on attempt_count
            cmd.CommandText = """
                UPDATE work_items
                SET status = CASE WHEN attempt_count >= @maxAttempts THEN 'DeadLetter' ELSE 'Pending' END,
                    failure_reason   = @reason,
                    exception_type   = @exType,
                    exception_detail = @exDetail,
                    completed_at     = @now,
                    started_at       = CASE WHEN attempt_count >= @maxAttempts THEN started_at ELSE NULL END
                WHERE item_id = @itemId
                """;
            AddParam(cmd, "@itemId",     itemId);
            AddParam(cmd, "@reason",     reason);
            AddParam(cmd, "@exType",     (object?)exceptionType   ?? DBNull.Value);
            AddParam(cmd, "@exDetail",   (object?)exceptionDetail ?? DBNull.Value);
            AddParam(cmd, "@maxAttempts", maxAttempts);
            AddParam(cmd, "@now",        DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Reporting ─────────────────────────────────────────────────────────────

        public async Task<QueueReport> GetReportAsync(string queueId, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            // Aggregate counts
            await using var aggCmd = conn.CreateCommand();
            aggCmd.CommandText = """
                SELECT
                    COUNT(*)                                                             AS total,
                    COUNT(*) FILTER (WHERE status = 'Pending')                          AS pending,
                    COUNT(*) FILTER (WHERE status = 'InProgress')                       AS in_progress,
                    COUNT(*) FILTER (WHERE status = 'Completed')                        AS completed,
                    COUNT(*) FILTER (WHERE status = 'Failed')                           AS failed,
                    COUNT(*) FILTER (WHERE status = 'DeadLetter')                       AS dead_letter,
                    AVG(EXTRACT(EPOCH FROM (completed_at - started_at)))
                        FILTER (WHERE status = 'Completed' AND started_at IS NOT NULL)  AS avg_seconds
                FROM work_items
                WHERE queue_id = @queueId
                """;
            AddParam(aggCmd, "@queueId", queueId);

            int total = 0, pending = 0, inProgress = 0, completed = 0, failed = 0, deadLetter = 0;
            double avgSeconds = 0;

            await using (var r = await aggCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                if (await r.ReadAsync(ct).ConfigureAwait(false))
                {
                    total      = r.GetInt32(0);
                    pending    = r.GetInt32(1);
                    inProgress = r.GetInt32(2);
                    completed  = r.GetInt32(3);
                    failed     = r.GetInt32(4);
                    deadLetter = r.GetInt32(5);
                    avgSeconds = r.IsDBNull(6) ? 0 : r.GetDouble(6);
                }
            }

            // Failed / dead-letter items
            var failedItems = await ReadItemsAsync(conn,
                "SELECT * FROM work_items WHERE queue_id = @queueId AND status IN ('Failed','DeadLetter')",
                queueId, ct).ConfigureAwait(false);

            // Runs
            var runs = await ReadRunsAsync(conn, queueId, ct).ConfigureAwait(false);

            var firstRun = runs.MinBy(r => r.StartedAt);
            var lastEnd  = runs.Where(r => r.EndedAt.HasValue).MaxBy(r => r.EndedAt);
            TimeSpan? elapsed = firstRun != null && lastEnd?.EndedAt != null
                ? lastEnd.EndedAt.Value - firstRun.StartedAt : null;

            return new QueueReport
            {
                QueueId               = queueId,
                TotalItems            = total,
                Pending               = pending,
                InProgress            = inProgress,
                Completed             = completed,
                Failed                = failed,
                DeadLetter            = deadLetter,
                AverageProcessingTime = TimeSpan.FromSeconds(avgSeconds),
                TotalElapsedTime      = elapsed,
                Runs                  = runs,
                FailedItems           = failedItems.Cast<IWorkItem>().ToList(),
                AllItems              = (await ReadItemsAsync(conn, "SELECT * FROM work_items WHERE queue_id = @queueId", queueId, ct).ConfigureAwait(false)).Cast<IWorkItem>().ToList()
            };
        }

        public async Task<int> GetPendingCountAsync(string queueId, CancellationToken ct = default)
            => await ScalarCountAsync("Pending", queueId, ct).ConfigureAwait(false);

        public async Task<int> GetInProgressCountAsync(string queueId, CancellationToken ct = default)
            => await ScalarCountAsync("InProgress", queueId, ct).ConfigureAwait(false);

        public async Task<int> GetDeadLetterCountAsync(string queueId, CancellationToken ct = default)
            => await ScalarCountAsync("DeadLetter", queueId, ct).ConfigureAwait(false);

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task<int> ScalarCountAsync(string status, string queueId, CancellationToken ct)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM work_items WHERE queue_id = @queueId AND status = @status";
            AddParam(cmd, "@queueId", queueId);
            AddParam(cmd, "@status",  status);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        }

        private async Task<List<WorkItem>> ReadItemsAsync(DbConnection conn, string sql, string queueId, CancellationToken ct)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParam(cmd, "@queueId", queueId);
            var results = new List<WorkItem>();
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                results.Add(ReadWorkItem(r));
            return results;
        }

        private async Task<List<QueueRun>> ReadRunsAsync(DbConnection conn, string queueId, CancellationToken ct)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT run_id, queue_id, bot_id, machine_name, started_at, ended_at FROM queue_runs WHERE queue_id = @queueId";
            AddParam(cmd, "@queueId", queueId);
            var results = new List<QueueRun>();
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(new QueueRun
                {
                    RunId       = r.GetString(0),
                    QueueId     = r.GetString(1),
                    BotId       = r.GetString(2),
                    MachineName = r.IsDBNull(3) ? "" : r.GetString(3),
                    StartedAt   = r.GetFieldValue<DateTimeOffset>(4),
                    EndedAt     = r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5)
                });
            }
            return results;
        }

        private static WorkItem ReadWorkItem(DbDataReader r)
        {
            var payloadJson = r.IsDBNull(r.GetOrdinal("payload")) ? "{}" : r.GetString(r.GetOrdinal("payload"));
            var outputJson  = r.IsDBNull(r.GetOrdinal("output"))  ? null : r.GetString(r.GetOrdinal("output"));

            return new WorkItem
            {
                ItemId          = r.GetString(r.GetOrdinal("item_id")),
                QueueId         = r.GetString(r.GetOrdinal("queue_id")),
                Reference       = r.IsDBNull(r.GetOrdinal("reference"))          ? null : r.GetString(r.GetOrdinal("reference")),
                Tag             = r.IsDBNull(r.GetOrdinal("tag"))                ? null : r.GetString(r.GetOrdinal("tag")),
                Priority        = r.GetInt32(r.GetOrdinal("priority")),
                AttemptCount    = r.GetInt32(r.GetOrdinal("attempt_count")),
                Status          = Enum.Parse<WorkItemStatus>(r.GetString(r.GetOrdinal("status"))),
                AssignedToBotId = r.IsDBNull(r.GetOrdinal("assigned_to_bot_id")) ? null : r.GetString(r.GetOrdinal("assigned_to_bot_id")),
                EnqueuedAt      = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("enqueued_at")),
                ScheduledAt     = r.IsDBNull(r.GetOrdinal("scheduled_at"))        ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("scheduled_at")),
                StartedAt       = r.IsDBNull(r.GetOrdinal("started_at"))          ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("started_at")),
                CompletedAt     = r.IsDBNull(r.GetOrdinal("completed_at"))        ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("completed_at")),
                FailureReason   = r.IsDBNull(r.GetOrdinal("failure_reason"))      ? null : r.GetString(r.GetOrdinal("failure_reason")),
                ExceptionType   = r.IsDBNull(r.GetOrdinal("exception_type"))      ? null : r.GetString(r.GetOrdinal("exception_type")),
                ExceptionDetail = r.IsDBNull(r.GetOrdinal("exception_detail"))    ? null : r.GetString(r.GetOrdinal("exception_detail")),
                Payload         = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson, _json) ?? new(),
                Output          = outputJson != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(outputJson, _json) : null
            };
        }

        private static void AddParam(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value         = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
