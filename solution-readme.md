# Valaiorp Execution Framework

A production-grade, modular, extensible **Workflow & Agentic AI Execution Framework** built in **.NET 10**. Designed for **enterprise-scale**, **multi-bot**, **multi-machine** deployments processing heavy transaction volumes from a shared work queue — with built-in retry, policy enforcement, execution logging, observability, and pluggable AI.

---

## The Four Workflow Types

Valaiorp processes work across four workflow types. The same runtime, tools, modules, and queue infrastructure are shared across all four modes. Switch modes by setting two config properties.

| Type | What it does | AI involved | Planner |
|------|-------------|-------------|---------|
| **IRPA** | Fixed, code-authored plan. Tools and modules execute exactly as defined. | None | `Deliberative` |
| **AI Workflow** | LLM produces the execution plan once. Steps then run deterministically. | Planning only | `LlmBased` |
| **AI Agent** | LLM plans and re-plans between steps based on intermediate results. | Planning + re-planning | `LlmBased` |
| **Agentic** | Fully autonomous — dynamic tool selection, self-directed re-planning, sub-agent spawning. | Full autonomy | `AutonomyAware` |

### AI Participation Modes (AI Workflow, AI Agent, Agentic only)

| Mode | What the AI does |
|------|-----------------|
| **Observe Only** | AI reads context, logs insight — no execution decisions made |
| **Observe & Suggest** | AI proposes next steps — a human or approval gate must confirm before execution |
| **Observe & React** | AI acts autonomously on what it observes |

### Configuration — two lines

```csharp
var config = new ValaiorpConfig
{
    WorkflowType    = WorkflowType.AiAgent,
    AiParticipation = AiParticipation.ObserveAndReact
}.ApplyProfile();
// ApplyProfile() sets PlannerType, AutonomyLevel, AllowDynamicPlanning,
// AllowToolSelection, and RequireApprovalForHighRisk automatically.
```

### Profile reference table

| WorkflowType | AiParticipation | PlannerType | Level | DynamicPlan | ToolSelect | ApprovalRequired |
|---|---|---|---|---|---|---|
| `Irpa` | any | `Deliberative` \| `Manual` | 0.0 | ✗ | ✗ | ✗ |
| `AiWorkflow` | `ObserveOnly` | `LlmBased` | 0.1 | ✗ | ✗ | ✓ |
| `AiWorkflow` | `ObserveAndSuggest` | `LlmBased` | 0.3 | ✗ | ✗ | ✓ |
| `AiWorkflow` | `ObserveAndReact` | `LlmBased` | 0.4 | ✓ | ✗ | ✓ |
| `AiAgent` | `ObserveOnly` | `LlmBased` | 0.4 | ✗ | ✗ | ✓ |
| `AiAgent` | `ObserveAndSuggest` | `LlmBased` | 0.6 | ✓ | ✗ | ✓ |
| `AiAgent` | `ObserveAndReact` | `LlmBased` | 0.7 | ✓ | ✓ | ✓ |
| `Agentic` | `ObserveOnly` | `AutonomyAware` | 0.7 | ✓ | ✗ | ✓ |
| `Agentic` | `ObserveAndSuggest` | `AutonomyAware` | 0.8 | ✓ | ✓ | ✓ |
| `Agentic` | `ObserveAndReact` | `AutonomyAware` | 1.0 | ✓ | ✓ | ✗ |

---

## Architecture

```
┌────────────────────────────────────────────────────────────────────────────┐
│                               Valaiorp                                     │
├─────────────┬───────────────┬──────────────┬──────────────┬───────────────┤
│    Core     │ Configuration │    Memory    │    Tools     │  BasicTools   │
├─────────────┼───────────────┼──────────────┼──────────────┼───────────────┤
│   Modules   │   Knowledge   │   Planner    │  Execution   │     Retry     │
├─────────────┼───────────────┼──────────────┼──────────────┼───────────────┤
│   Logging   │ Observability │ LlmProviders │  MultiAgent  │  Escalation   │
├─────────────┼───────────────┼──────────────┴──────────────┴───────────────┤
│   Policy    │  Guardrails   │                                               │
├─────────────┴───────────────┴──────────────────────────────────────────────┤
│                               Runtime                                      │
│     AgentRuntime · BotWorker · InMemoryWorkQueue · JsonlWorkQueue          │
│     SqlWorkQueue (abstract, plug in any ADO.NET provider)                  │
└────────────────────────────────────────────────────────────────────────────┘
```

### Multi-Bot / Multi-Machine Deployment

```
  ┌───────────────────┐   ┌───────────────────┐   ┌───────────────────┐
  │    BotWorker      │   │    BotWorker      │   │    BotWorker      │
  │  "sap-bot" · A   │   │  "sap-bot" · B   │   │  "sap-bot" · C   │
  │  Machine 1        │   │  Machine 2        │   │  Machine 3        │
  └────────┬──────────┘   └────────┬──────────┘   └────────┬──────────┘
           │                       │                        │
           └───────────────────────┼────────────────────────┘
                                   │
                          ┌────────▼─────────┐
                          │   IWorkQueue     │
                          │                  │
                          │  InMemory (dev)  │
                          │  JSONL  (local)  │
                          │  SQL    (prod)   │  ← SQL Server, PostgreSQL, SQLite
                          └──────────────────┘
```

All bots compete for items from the same queue. `SqlWorkQueue` uses `SELECT FOR UPDATE SKIP LOCKED` so no two bots ever claim the same transaction.

---

## Work Queue Operations

The queue is the coordination layer between producers (systems that create work) and consumers (bots that execute it).

```csharp
IWorkQueue queue = new SqlWorkQueue(...);  // or InMemoryWorkQueue / JsonlWorkQueue

// ── Producer side ────────────────────────────────────────────────────────

// Bulk-populate a queue with transactions (idempotent by Reference)
await queue.PopulateAsync("sap-invoices", new[]
{
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-001", Priority = 10, Payload = new() { ["amount"] = 5000 } },
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-002", Priority = 5,  Payload = new() { ["amount"] = 1200 } },
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-003", Tag = "urgent", Priority = 20, Payload = new() { ["amount"] = 99000 } }
});

// Optionally pre-assign all items to a specific bot
await queue.AssignQueueToBotAsync("sap-invoices", "bot-A");

// ── Bot side (BotWorker does this automatically) ──────────────────────────

// Start a run (audit trail)
var run = await queue.StartRunAsync("sap-invoices", "bot-A");

// Claim the next available item — atomic, multi-machine safe
var item = await queue.GetNextItemAsync("sap-invoices", botId: "bot-A");
var item = await queue.GetNextItemAsync("sap-invoices", tag: "urgent");
var item = await queue.GetNextItemAsync("sap-invoices", reference: "INV-001");

// Mark outcome
await queue.MarkCompletedAsync(item.ItemId, output: new() { ["postingDoc"] = "5000234" });

await queue.MarkFailedAsync(item.ItemId,
    reason:          "SAP login timed out",
    exceptionType:   "TimeoutException",
    exceptionDetail: ex.ToString(),
    maxAttempts:     3);   // re-queued if attempts < 3, dead-lettered if >= 3

// End run
await queue.EndRunAsync(run.RunId);

// ── Reporting ────────────────────────────────────────────────────────────

var report = await queue.GetReportAsync("sap-invoices");
Console.WriteLine($"Total:      {report.TotalItems}");
Console.WriteLine($"Completed:  {report.Completed}  ({report.SuccessRate}%)");
Console.WriteLine($"Failed:     {report.Failed}");
Console.WriteLine($"DeadLetter: {report.DeadLetter}");
Console.WriteLine($"Avg time:   {report.AverageProcessingTime.TotalSeconds:F1}s");
Console.WriteLine($"Runs:       {report.Runs.Count}");
foreach (var (exType, count) in report.FailuresByExceptionType)
    Console.WriteLine($"  {exType}: {count}");
```

### WorkItem properties

| Property | Type | Description |
|---|---|---|
| `ItemId` | `string` | Auto-generated GUID |
| `QueueId` | `string` | Queue this item belongs to |
| `Reference` | `string?` | External key — invoice number, case ID, etc. Unique per queue. |
| `Tag` | `string?` | Grouping / routing label |
| `Priority` | `int` | Higher = processed first |
| `AttemptCount` | `int` | Auto-incremented on each `GetNextItemAsync` call |
| `Status` | `WorkItemStatus` | `Pending` → `InProgress` → `Completed` / `Failed` / `DeadLetter` |
| `AssignedToBotId` | `string?` | Set by `AssignQueueToBotAsync` or `GetNextItemAsync` |
| `ScheduledAt` | `DateTimeOffset?` | Delayed execution — item won't be claimed until this time |
| `FailureReason` | `string?` | Last failure message |
| `ExceptionType` | `string?` | Exception class name (e.g. `TimeoutException`) |
| `ExceptionDetail` | `string?` | Full exception `ToString()` |
| `Payload` | `IDictionary<string,object>` | Input data for the bot |
| `Output` | `IDictionary<string,object>?` | Output data written on completion |

---

## Queue Backends

### InMemoryWorkQueue
- In-process only — no persistence
- For dev, testing, and single-process bots
- No configuration needed

```csharp
IWorkQueue queue = new InMemoryWorkQueue();
```

### JsonlWorkQueue
- JSONL files on disk — one file per queue
- Survives process restarts
- Single local bot only (file locking, not multi-machine safe)

```csharp
IWorkQueue queue = new JsonlWorkQueue(directory: "C:\\BotQueues");
// Creates: C:\BotQueues\sap-invoices.queue.jsonl
//          C:\BotQueues\sap-invoices.runs.jsonl
```

### SqlWorkQueue
- ADO.NET abstract base — works with any SQL provider
- Multi-machine safe via `SELECT FOR UPDATE SKIP LOCKED`
- Subclass and override `CreateConnection()`:

```csharp
// SQL Server
public sealed class MyQueue : SqlWorkQueue
{
    protected override DbConnection CreateConnection()
        => new SqlConnection("Server=prod-db;Database=BotQueue;Integrated Security=true");
}

// PostgreSQL
public sealed class MyQueue : SqlWorkQueue
{
    protected override DbConnection CreateConnection()
        => new NpgsqlConnection("Host=prod-db;Database=bot_queue;Username=bot;Password=...");
}

// SQLite (local single-machine, but still file-persisted)
public sealed class MyQueue : SqlWorkQueue
{
    protected override DbConnection CreateConnection()
        => new SqliteConnection("Data Source=botqueue.db");
}

// Create schema once at startup
var queue = new MyQueue();
await queue.CreateSchemaAsync();
```

#### SQL Schema (auto-created by `CreateSchemaAsync()`)

```sql
CREATE TABLE queue_runs (
    run_id       VARCHAR(100)  PRIMARY KEY,
    queue_id     VARCHAR(100)  NOT NULL,
    bot_id       VARCHAR(100)  NOT NULL,
    machine_name VARCHAR(200),
    started_at   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at     TIMESTAMP     NULL
);

CREATE TABLE work_items (
    item_id            VARCHAR(100)  PRIMARY KEY,
    queue_id           VARCHAR(100)  NOT NULL,
    reference          VARCHAR(500)  NULL,
    tag                VARCHAR(200)  NULL,
    priority           INTEGER       NOT NULL DEFAULT 0,
    attempt_count      INTEGER       NOT NULL DEFAULT 0,
    status             VARCHAR(50)   NOT NULL DEFAULT 'Pending',
    assigned_to_bot_id VARCHAR(100)  NULL,
    enqueued_at        TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    scheduled_at       TIMESTAMP     NULL,
    started_at         TIMESTAMP     NULL,
    completed_at       TIMESTAMP     NULL,
    failure_reason     TEXT          NULL,
    exception_type     VARCHAR(500)  NULL,
    exception_detail   TEXT          NULL,
    payload            TEXT          NULL,   -- JSON
    output             TEXT          NULL    -- JSON
);

-- Composite index for fast bot polling
CREATE INDEX idx_work_items_queue_status
    ON work_items(queue_id, status, priority DESC, enqueued_at ASC);

-- Unique reference per queue (idempotent population)
CREATE UNIQUE INDEX idx_work_items_ref
    ON work_items(queue_id, reference) WHERE reference IS NOT NULL;
```

---

## BotWorker — Multi-Bot Runtime

`BotWorker` is a self-contained execution unit: it owns a `AgentRuntime`, polls the queue, processes items concurrently, and handles retry and dead-letter automatically.

```csharp
// Build the bot (does not start yet)
await using var bot = RuntimeBuilder.BuildBot(
    queueId:        "sap-invoices",
    botId:          "sap-bot",
    config:         new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile(),
    queue:          new MyQueue(),          // shared SqlWorkQueue — same instance on all machines
    maxConcurrency: 8,                      // items processed in parallel on this machine
    maxAttempts:    3,                      // retries before dead-letter
    configureModules: m => m.Register(new SapLoginModule()));

// Register the planner that converts queue payload into a Plan
var planner = bot.GetService<PlannerOrchestrator>(); // via AgentRuntime.GetService<T>()
planner.RegisterPlanner(new SapInvoicePlanner(), setAsDefault: true);

// Start polling (non-blocking)
await bot.StartAsync();

// Monitor
Console.WriteLine($"Pending:    {await bot.GetPendingCountAsync()}");
Console.WriteLine($"InProgress: {await bot.GetInProgressCountAsync()}");
Console.WriteLine($"DeadLetter: {await bot.GetDeadLetterCountAsync()}");

var report = await bot.GetReportAsync();
Console.WriteLine($"Success rate: {report.SuccessRate}%");

// Graceful shutdown — drains in-flight items, ends the queue run
await bot.StopAsync();
```

Deploy the same binary on N machines, all pointing at the same `SqlWorkQueue`. No other coordination needed.

---

## Getting Started

### IRPA — Pure Automation (no AI)

```csharp
using Valaiorp.Configuration.Config;
using Valaiorp.Core.Enums;
using Valaiorp.Runtime;
using Valaiorp.Runtime.Bootstrap;

var config = new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile();

await using var runtime = RuntimeBuilder.Build(config);
runtime.GetService<PlannerOrchestrator>().RegisterPlanner(new MySapWorkflowPlanner(), setAsDefault: true);

var result = await runtime.ExecuteAsync(new MyExecutionContext());
```

### AI Workflow — LLM Plans, Fixed Execution

```csharp
var config = new ValaiorpConfig
{
    WorkflowType    = WorkflowType.AiWorkflow,
    AiParticipation = AiParticipation.ObserveAndReact,
    Llm = new LlmConfig { Provider = "anthropic", ModelId = "claude-sonnet-4-6", ApiKeyEnvVar = "ANTHROPIC_API_KEY" }
}.ApplyProfile();

await using var runtime = RuntimeBuilder.Build(config);
```

### AI Agent — LLM Plans and Re-plans

```csharp
var config = new ValaiorpConfig
{
    WorkflowType    = WorkflowType.AiAgent,
    AiParticipation = AiParticipation.ObserveAndReact,
    Llm = new LlmConfig { Provider = "anthropic", ModelId = "claude-sonnet-4-6", ApiKeyEnvVar = "ANTHROPIC_API_KEY" }
}.ApplyProfile();
```

### Agentic — Full Autonomy

```csharp
var config = new ValaiorpConfig
{
    WorkflowType    = WorkflowType.Agentic,
    AiParticipation = AiParticipation.ObserveAndReact,
    Llm = new LlmConfig { Provider = "anthropic", ModelId = "claude-sonnet-4-6", ApiKeyEnvVar = "ANTHROPIC_API_KEY" }
}.ApplyProfile();
```

---

## Retry

Two independent retry layers.

### Tool-level (automatic, per tool call)

| Policy | Default | Behaviour |
|--------|---------|-----------|
| `MaxAttemptsRetryPolicy` | 3 attempts | Hard cap |
| `ExponentialBackoffRetryPolicy` | 100 ms → 10 s | Doubles delay each attempt |
| `CircuitBreakerRetryPolicy` | 5 failures / 30 s reset | Opens after repeated failures |

### Queue-level (automatic, per work item)

| Outcome | Action |
|---------|--------|
| `IsSuccess = true` | `MarkCompletedAsync` — item removed from queue |
| `IsSuccess = false`, `AttemptCount < maxAttempts` | `MarkFailedAsync` — item re-queued with incremented count |
| `IsSuccess = false`, `AttemptCount >= maxAttempts` | `MarkFailedAsync` — item moved to `DeadLetter` |

---

## Execution Logging

Automatic — three entries written per execution.

| Event | Trigger |
|-------|---------|
| **Plan** | After plan creation |
| **Step** | After each step completes |
| **Run** | After executor finishes |

---

## Customisation

### Custom Tool

```csharp
public sealed class SapPostTool : ITool
{
    public string Id          => "sap-post";
    public string Name        => "SAP Post";
    public string Description => "Posts an invoice to SAP. Parameters: invoiceId, amount, costCenter.";
    public ToolType Type      => ToolType.Custom;
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();

    public async Task<ToolResult> ExecuteAsync(
        IExecutionContext context,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct = default)
    {
        var invoiceId  = parameters.GetString("invoiceId");
        var amount     = parameters.GetInt("amount");
        var costCenter = parameters.GetString("costCenter");
        var docNumber  = await SapClient.PostAsync(invoiceId, amount, costCenter, ct);
        return ToolResult.Ok(new { documentNumber = docNumber });
    }
}
```

### Custom Module (SAP Login)

```csharp
public sealed class SapLoginModule : BaseModule
{
    public override string Id          => "sap-login";
    public override string Name        => "SAP Login";
    public override string Description => "Opens SAP, connects to server, and logs in. Parameters: server, username, password.";

    public override IReadOnlyDictionary<string, ParameterDefinition> Parameters => new Dictionary<string, ParameterDefinition>
    {
        ["server"]   = new() { Name = "server",   Type = "string", Required = true, Description = "SAP server ID" },
        ["username"] = new() { Name = "username", Type = "string", Required = true, Description = "SAP username" },
        ["password"] = new() { Name = "password", Type = "string", Required = true, Description = "SAP password" }
    };

    public override IReadOnlyCollection<ITool> Tools { get; } = /* inject from ToolRegistry */

    protected override IReadOnlyDictionary<string, object> BuildToolParameters(ITool tool, IReadOnlyDictionary<string, object> p)
        => tool.Id switch
        {
            "ui-set-server"  => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtServer",   ["value"] = p["server"] },
            "ui-set-user"    => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtUsername", ["value"] = p["username"] },
            "ui-set-pass"    => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtPassword", ["value"] = p["password"] },
            "ui-click-enter" => new Dictionary<string, object> { ["action"] = "ClickButton", ["element"] = "OK" },
            _                => p
        };
}
```

### Custom Planner (IRPA fixed workflow with variable binding)

```csharp
public sealed class SapInvoicePlanner : IPlanner
{
    public string Id => "sap-invoice";
    public PlannerType Type => PlannerType.Deliberative;
    public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;

    public Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        => Task.FromResult(new Plan
        {
            ContextId = context.Id,
            Steps = new[]
            {
                new PlanStep { Name = "Login",   ToolId = "sap-login",  Inputs = new Dictionary<string, object> { ["server"] = context.Metadata["server"], ["username"] = "bot_user", ["password"] = Environment.GetEnvironmentVariable("SAP_PASS")! } },
                new PlanStep { Name = "Post",    ToolId = "sap-post",   Inputs = new Dictionary<string, object> { ["invoiceId"] = context.Metadata["reference"], ["amount"] = context.Metadata["amount"], ["costCenter"] = "CC-100" } },
                new PlanStep { Name = "Export",  ToolId = "excel-tool", Inputs = new Dictionary<string, object> { ["operation"] = "writecell", ["filePath"] = @"C:\Reports\log.xlsx", ["cell"] = "A1", ["value"] = "${Post.Results.documentNumber}" } },
                new PlanStep { Name = "Logout",  ToolId = "sap-logout", Inputs = new Dictionary<string, object>() }
            }
        });
}
```

---

## Project Structure

```
valaiorp/
├── Core/               # IWorkItem, IWorkQueue, IBotContext, QueueRun, QueueReport,
│                       # ITool, IModule, IExecutionContext, enums, errors
├── Configuration/      # WorkflowType, AiParticipation, WorkflowProfile, ValaiorpConfig,
│                       # PlannerConfig, AutonomyConfig, LlmConfig, …
├── Memory/             # Short-term, long-term, conversation memory
├── Tools/              # ToolRegistry, ModuleRegistry, ToolResolver, ToolParameters helpers
├── BasicTools/         # 10 built-in file formats, folder, HTTP API, UIAutomation, Playwright
├── Modules/            # BaseModule, ModuleTool, ModuleExecutor
├── Knowledge/          # IKnowledgeProvider — RAG integration (vector store, SQL, custom DLL)
├── Policy/             # PolicyRule, IPolicyEngine — pre/post execution governance
├── Guardrails/         # IGuardrail, IGuardrailPipeline, GuardrailPipeline,
│                       # PiiGuardrail, PromptInjectionGuardrail, BannedKeywordsGuardrail,
│                       # ContentLengthGuardrail, ToolScopeGuardrail, DataClassificationGuardrail
├── Planner/            # InternalPlanner, LlmPlanner, AutonomyAwarePlanner, ManualPlanner,
│                       # PlannerOrchestrator, PlanEvaluator, IPlanEvaluator,
│                       # plan.schema.json, plan.sample.json
├── Execution/          # ParallelExecutor, WorkflowExecutor, variable binding, transactions,
│                       # ReplayEngine, StepSnapshot, ExecutionSnapshot,
│                       # AgentBudget, BudgetTracker, BudgetExceededException
├── Retry/              # MaxAttemptsPolicy, ExponentialBackoffPolicy, CircuitBreakerPolicy
├── Logging/            # Plan/step/run logging — in-memory or JSONL files
├── Observability/      # Console logger, tracing, metrics
├── LlmProviders/       # GenericLlmClient + 7 built-in JSON profiles (Anthropic, OpenAI, Ollama, Gemini, Mistral, Cohere, NVIDIA)
├── MultiAgent/         # IAgent, IAgentRegistry, MultiAgentOrchestrator
├── Escalation/         # IApprovalProvider, IOverrideProvider, IEscalationHandler
└── Runtime/            # AgentRuntime, BotWorker, InMemoryWorkQueue, JsonlWorkQueue,
                        # SqlWorkQueue, BotContext, RuntimeBuilder, DI wiring
```

---

## Key Features

| Feature | Description |
|---------|-------------|
| **4 Workflow Types** | IRPA → AI Workflow → AI Agent → Agentic — one framework, one config switch |
| **3 AI Participation Modes** | Observe Only · Observe & Suggest · Observe & React |
| **WorkflowProfile presets** | `ApplyProfile()` sets all planner and autonomy flags from two enums |
| **Multi-Bot / Multi-Machine** | BotWorker + shared IWorkQueue — N bots, N machines, zero coordination code |
| **3 Queue Backends** | InMemory (dev) · JSONL (local) · SQL (production) |
| **Priority Queue** | Higher `Priority` items are processed first |
| **Queue Operations** | StartRun · EndRun · Populate · Assign · GetNext · MarkCompleted · MarkFailed · GetReport |
| **Atomic Item Claim** | `SELECT FOR UPDATE SKIP LOCKED` — no duplicate processing across bots |
| **2-Layer Retry** | Tool-level (exponential backoff + circuit breaker) + Queue-level (nack/dead-letter) |
| **Dead-Letter** | Failed items after max attempts parked with reason, exception type, and detail |
| **Transaction Report** | Total · Pending · InProgress · Completed · Failed · DeadLetter · AvgTime · SuccessRate · FailuresByExceptionType |
| **4 Planner Types** | `Deliberative` (code) · `Manual` (JSON) · `LlmBased` · `AutonomyAware` |
| **Manual Plan** | Supply a JSON plan file — `plan.schema.json` + `plan.sample.json` for dev testing |
| **Variable Binding** | `${StepName.Results.Field}` resolved between plan steps at runtime |
| **10 Built-in File Formats** | JSON · JSONL · JSONC · TXT · CSV · TSV · PSV · XML · XLSX · DOCX — no extra NuGet packages |
| **Built-in Tools** | HTTP API · Folder ops · Windows UIAutomation (Win32/WPF/WinForms) · Playwright browser automation (opt-in) |
| **Modules** | Reusable multi-step tool sequences — visible to the LLM for planning |
| **Policy Enforcement** | Pre/post-execution governance rules — runs at execution-unit scope |
| **Guardrails** | Content-level safety — PII redaction, prompt injection, banned keywords, tool scope, data classification |
| **6 Built-in Guardrails** | `PiiGuardrail` · `PromptInjectionGuardrail` · `BannedKeywordsGuardrail` · `ContentLengthGuardrail` · `ToolScopeGuardrail` · `DataClassificationGuardrail` |
| **Guardrail Pipeline** | Block → Redact (chains sanitised content) → Warn → Escalate — first Block wins |
| **Human Escalation** | Approval workflows · override hooks · manual intervention |
| **Multi-Agent** | Orchestrator/sub-agent delegation with parallel dispatch and conversation memory |
| **Execution Replay** | Per-step state snapshots — deterministic re-run from any step; time-travel debugging via `ReplayEngine` |
| **Agent Budgeting** | Max tool calls, max tokens, and max execution time per workflow — `BudgetTracker` enforces limits at runtime |
| **Planner Evaluation** | Confidence scoring (0–1) + structural validation before execution — `Proceed / Review / Reject` recommendation via `PlanEvaluator` |
| **File-backed Memory** | Short-term, long-term, and conversation memory backed by JSONL files by default — swap for Redis/SQL |
| **SQL Logging** | `AddSqlPersistence()` layers SQL execution logging alongside mandatory local JSONL logs |
| **LLM Providers** | 7 built-in profiles: Anthropic · OpenAI · Ollama · Gemini · Mistral · Cohere · NVIDIA — single `GenericLlmClient`, no vendor SDK |
| **Config File** | Load `ValaiorpConfig` from `valaiorp.json` via `RuntimeBuilder.BuildFromFile(path)` |
| **Async Throughout** | Non-blocking end-to-end |
| **Clean Architecture** | Interface-driven, strictly layered, fully DI-compatible |

---

## License

GPLv3. See [LICENSE](LICENSE) for details.
