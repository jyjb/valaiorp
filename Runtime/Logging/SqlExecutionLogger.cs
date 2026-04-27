namespace Valaiorp.Runtime.Logging
{
    using System.Data.Common;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Enhanced.Logging;

    /// <summary>
    /// SQL-backed execution logger. Works with any ADO.NET-compatible provider
    /// (SQL Server, PostgreSQL, SQLite, MySQL, Oracle).
    ///
    /// Subclass and override CreateConnection() to supply the connection, or use
    /// the factory constructor for inline registration:
    ///   services.AddSqlPersistence(() => new SqlConnection(connStr));
    ///
    /// Call CreateSchemaAsync() once at startup to create the execution_logs table.
    /// </summary>
    public abstract class SqlExecutionLogger : IExecutionLogger
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented        = false
        };

        // ── Schema DDL ────────────────────────────────────────────────────────────

        /// <summary>Creates the execution_logs table if it does not exist. Call once at startup.</summary>
        public async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS execution_logs (
                    log_id        VARCHAR(100) PRIMARY KEY,
                    log_type      VARCHAR(20)  NOT NULL,
                    context_id    VARCHAR(100) NOT NULL,
                    entity_id     VARCHAR(100) NOT NULL,
                    entity_name   VARCHAR(500),
                    logged_at     TIMESTAMP    NOT NULL,
                    status        VARCHAR(50),
                    ai_used       BOOLEAN      NOT NULL DEFAULT FALSE,
                    input_tokens  INTEGER,
                    output_tokens INTEGER,
                    total_tokens  INTEGER,
                    model_id      VARCHAR(200),
                    error         TEXT,
                    payload       TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_exec_logs_context
                    ON execution_logs(context_id, log_type, logged_at DESC);
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // ── Abstract factory ──────────────────────────────────────────────────────

        protected abstract DbConnection CreateConnection();

        // ── IExecutionLogger ──────────────────────────────────────────────────────

        public async Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default)
        {
            var payload = new
            {
                plan.Id,
                plan.ContextId,
                AiUsed = plan.PlanningTokens != null,
                PlanningTokens = plan.PlanningTokens != null ? new
                {
                    plan.PlanningTokens.InputTokens,
                    plan.PlanningTokens.OutputTokens,
                    plan.PlanningTokens.TotalTokens,
                    plan.PlanningTokens.ModelId
                } : null,
                Steps = plan.Steps.Select(s => new { s.Id, s.Name, s.Description, s.ToolId, s.ModuleId })
            };

            await InsertLogAsync(
                logType:    "Plan",
                contextId:  context.Id,
                entityId:   plan.Id,
                entityName: null,
                loggedAt:   DateTimeOffset.UtcNow,
                status:     null,
                aiUsed:     plan.PlanningTokens != null,
                inputTok:   plan.PlanningTokens?.InputTokens,
                outputTok:  plan.PlanningTokens?.OutputTokens,
                totalTok:   plan.PlanningTokens?.TotalTokens,
                modelId:    plan.PlanningTokens?.ModelId,
                error:      null,
                payload:    JsonSerializer.Serialize(payload, _json),
                ct:         ct).ConfigureAwait(false);
        }

        public async Task LogRunAsync(ExecutionUnit unit, IExecutionContext context, CancellationToken ct = default)
        {
            await InsertLogAsync(
                logType:    "Run",
                contextId:  context.Id,
                entityId:   unit.Id,
                entityName: null,
                loggedAt:   DateTimeOffset.UtcNow,
                status:     unit.Status.ToString(),
                aiUsed:     false,
                inputTok:   null,
                outputTok:  null,
                totalTok:   null,
                modelId:    null,
                error:      unit.Exception?.Message,
                payload:    null,
                ct:         ct).ConfigureAwait(false);
        }

        public async Task LogStepAsync(TaskNode node, IExecutionContext context, CancellationToken ct = default)
        {
            await InsertLogAsync(
                logType:    "Step",
                contextId:  context.Id,
                entityId:   node.Id,
                entityName: node.Step.Name,
                loggedAt:   DateTimeOffset.UtcNow,
                status:     node.Status.ToString(),
                aiUsed:     node.AiUsed,
                inputTok:   node.LlmTokens?.InputTokens,
                outputTok:  node.LlmTokens?.OutputTokens,
                totalTok:   node.LlmTokens?.TotalTokens,
                modelId:    node.LlmTokens?.ModelId,
                error:      node.Exception?.Message,
                payload:    null,
                ct:         ct).ConfigureAwait(false);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private async Task InsertLogAsync(
            string logType, string contextId, string entityId, string? entityName,
            DateTimeOffset loggedAt, string? status, bool aiUsed,
            int? inputTok, int? outputTok, int? totalTok, string? modelId,
            string? error, string? payload, CancellationToken ct)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO execution_logs
                    (log_id, log_type, context_id, entity_id, entity_name, logged_at,
                     status, ai_used, input_tokens, output_tokens, total_tokens,
                     model_id, error, payload)
                VALUES
                    (@logId, @logType, @contextId, @entityId, @entityName, @loggedAt,
                     @status, @aiUsed, @inputTok, @outputTok, @totalTok,
                     @modelId, @error, @payload)
                ON CONFLICT (log_id) DO NOTHING
                """;

            AddParam(cmd, "@logId",      Guid.NewGuid().ToString("N"));
            AddParam(cmd, "@logType",    logType);
            AddParam(cmd, "@contextId",  contextId);
            AddParam(cmd, "@entityId",   entityId);
            AddParam(cmd, "@entityName", (object?)entityName  ?? DBNull.Value);
            AddParam(cmd, "@loggedAt",   loggedAt);
            AddParam(cmd, "@status",     (object?)status      ?? DBNull.Value);
            AddParam(cmd, "@aiUsed",     aiUsed);
            AddParam(cmd, "@inputTok",   (object?)inputTok    ?? DBNull.Value);
            AddParam(cmd, "@outputTok",  (object?)outputTok   ?? DBNull.Value);
            AddParam(cmd, "@totalTok",   (object?)totalTok    ?? DBNull.Value);
            AddParam(cmd, "@modelId",    (object?)modelId     ?? DBNull.Value);
            AddParam(cmd, "@error",      (object?)error       ?? DBNull.Value);
            AddParam(cmd, "@payload",    (object?)payload     ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private static void AddParam(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value         = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    /// <summary>
    /// Concrete SqlExecutionLogger backed by an inline connection factory.
    /// Used internally by services.AddSqlPersistence(factory).
    /// </summary>
    internal sealed class InlineSqlExecutionLogger : SqlExecutionLogger
    {
        private readonly Func<DbConnection> _factory;
        public InlineSqlExecutionLogger(Func<DbConnection> factory) => _factory = factory;
        protected override DbConnection CreateConnection() => _factory();
    }
}
