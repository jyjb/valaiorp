# Valaiorp — Usage Guide

Consuming the Valaiorp framework as compiled assemblies in a .NET 10 project.

---

## Required Assemblies

| Assembly | Purpose |
|----------|---------|
| `Valaiorp.Core.dll` | Contracts (`IWorkItem`, `IWorkQueue`, `IBotContext`, `ITool`, `IModule`, …), enums, errors — always required |
| `Valaiorp.Configuration.dll` | `ValaiorpConfig`, `WorkflowProfile`, all config models |
| `Valaiorp.Runtime.dll` | `AgentRuntime`, `BotWorker`, queue implementations, `RuntimeBuilder` — always required |
| `Valaiorp.Memory.dll` | Short-term, long-term, and conversation memory |
| `Valaiorp.Tools.dll` | `ToolRegistry`, `ModuleRegistry`, `ToolResolver`, `ToolParameters` helpers |
| `Valaiorp.BasicTools.dll` | 10 built-in file/folder/API/UI tools |
| `Valaiorp.Modules.dll` | `BaseModule`, `ModuleTool`, `ModuleExecutor` |
| `Valaiorp.Policy.dll` | `PolicyRule`, `IPolicyEngine` |
| `Valaiorp.Planner.dll` | `IPlanner`, `Plan`, `PlannerOrchestrator` |
| `Valaiorp.Knowledge.dll` | `IKnowledgeProvider`, resolver |
| `Valaiorp.Execution.dll` | Execution engine, variable binding, transactions |
| `Valaiorp.Observability.dll` | Console logging, tracing, metrics |
| `Valaiorp.Retry.dll` | Retry policies — auto-registered by Runtime |
| `Valaiorp.Logging.dll` | Plan/step/run logging — auto-registered by Runtime |
| `Valaiorp.LlmProviders.dll` | `GenericLlmClient` + 7 built-in JSON profiles (Anthropic, OpenAI, Ollama, Gemini, Mistral, Cohere, NVIDIA) |
| `Valaiorp.MultiAgent.dll` | Multi-agent registry and orchestration |
| `Valaiorp.Escalation.dll` | Approval, override, and escalation handling |
| `Valaiorp.Guardrails.dll` | 6 built-in safety guardrails — PII redaction, prompt injection, banned keywords, content length, tool scope, data classification |

### .csproj reference (local DLLs)

```xml
<ItemGroup>
  <Reference Include="Valaiorp.Core">
    <HintPath>lib\Valaiorp.Core.dll</HintPath>
  </Reference>
  <Reference Include="Valaiorp.Configuration">
    <HintPath>lib\Valaiorp.Configuration.dll</HintPath>
  </Reference>
  <Reference Include="Valaiorp.Runtime">
    <HintPath>lib\Valaiorp.Runtime.dll</HintPath>
  </Reference>
  <!-- add remaining DLLs as needed -->
</ItemGroup>
```

---

## Workflow Types and Configuration

Two enums + one call configures the entire framework:

```csharp
using Valaiorp.Configuration.Config;
using Valaiorp.Core.Enums;

var config = new ValaiorpConfig
{
    WorkflowType    = WorkflowType.AiAgent,       // Irpa | AiWorkflow | AiAgent | Agentic
    AiParticipation = AiParticipation.ObserveAndReact  // ObserveOnly | ObserveAndSuggest | ObserveAndReact
}.ApplyProfile();
// ApplyProfile() fills Planner.Type, Autonomy.Level, AllowDynamicPlanning,
// AllowToolSelection, and RequireApprovalForHighRisk automatically.
// You can override any of these manually after the call.
```

| WorkflowType | AiParticipation | PlannerType | AutonomyLevel | DynamicPlan | ToolSelect | NeedsApproval |
|---|---|---|---|---|---|---|
| `Irpa` | (any) | `Deliberative` | 0.0 | ✗ | ✗ | ✗ |
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

## Quickstart — Single Execution (no queue)

### 1. Implement `IExecutionContext`

```csharp
using Valaiorp.Core.Contracts;

public sealed class MyExecutionContext : IExecutionContext
{
    public string Id        { get; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public string UserId    { get; init; } = "anonymous";
    public DateTimeOffset CreatedAt  { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
    public IDictionary<string, object>    Metadata { get; init; } = new Dictionary<string, object>();
    public IReadOnlyCollection<IExecutionStep> Steps { get; init; } = [];  // C# 12; use new List<IExecutionStep>() for earlier versions
    public CancellationToken CancellationToken { get; init; }
    public PromptContext? Prompt { get; set; }
}
```

### 2. Build and run

```csharp
using Valaiorp.Runtime.Bootstrap;
using Valaiorp.Configuration.Config;

var config = new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile();

// RuntimeBuilder.Build() calls config.Validate(), registers all services,
// and auto-registers all BasicTools — no manual setup needed.
await using var runtime = RuntimeBuilder.Build(config);

// Register your workflow planner
runtime.GetService<PlannerOrchestrator>()
       .RegisterPlanner(new MyWorkflowPlanner(), setAsDefault: true);

var result = await runtime.ExecuteAsync(new MyExecutionContext { UserId = "user-1" });

Console.WriteLine(result.IsSuccess
    ? $"Done in {result.ExecutionTime.TotalSeconds:F2}s"
    : $"Failed: {result.ErrorMessage}");
```

### Load config from file

```csharp
// valaiorp.json is deserialized into ValaiorpConfig — same shape as the code API
await using var runtime = RuntimeBuilder.BuildFromFile("valaiorp.json");
```

---

## Work Queue — Multi-Bot Execution

The queue is the coordination layer between producers and bots. All bots sharing the same queue backend compete for items automatically — no coordination code required.

### Queue backends

| Class | Backend | Use for |
|---|---|---|
| `InMemoryWorkQueue` | In-process | Dev, testing, single process |
| `JsonlWorkQueue` | JSONL files | Single local bot, survives restarts |
| `SqlWorkQueue` | SQL (any ADO.NET) | Multi-machine production |

### Choosing a queue

```csharp
// Dev / testing
IWorkQueue queue = new InMemoryWorkQueue();

// Local single bot (no DB needed)
IWorkQueue queue = new JsonlWorkQueue(directory: "C:\\BotQueues");

// Production — SQL Server
public sealed class MyQueue : SqlWorkQueue
{
    protected override DbConnection CreateConnection()
        => new SqlConnection("Server=prod;Database=BotQueue;Integrated Security=true");
}

// Production — PostgreSQL
public sealed class MyQueue : SqlWorkQueue
{
    protected override DbConnection CreateConnection()
        => new NpgsqlConnection("Host=prod;Database=bot_queue;Username=bot;Password=...");
}

IWorkQueue queue = new MyQueue();
await ((SqlWorkQueue)queue).CreateSchemaAsync();  // run once at startup
```

### SQL schema (created by `CreateSchemaAsync()`)

The schema below uses ANSI-portable types. `CreateSchemaAsync()` emits provider-specific DDL automatically — for PostgreSQL, `payload`/`output` are created as `JSONB` and GUIDs as `UUID`; for SQL Server, `TIMESTAMP` becomes `DATETIMEOFFSET`.

```sql
CREATE TABLE queue_runs (
    run_id       VARCHAR(100) PRIMARY KEY,
    queue_id     VARCHAR(100) NOT NULL,
    bot_id       VARCHAR(100) NOT NULL,
    machine_name VARCHAR(200),
    started_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at     TIMESTAMP    NULL
);

CREATE TABLE work_items (
    item_id            VARCHAR(100) PRIMARY KEY,
    queue_id           VARCHAR(100) NOT NULL,
    reference          VARCHAR(500) NULL,        -- unique per queue, idempotent population
    tag                VARCHAR(200) NULL,
    priority           INTEGER      NOT NULL DEFAULT 0,
    attempt_count      INTEGER      NOT NULL DEFAULT 0,
    status             VARCHAR(50)  NOT NULL DEFAULT 'Pending',
    assigned_to_bot_id VARCHAR(100) NULL,
    enqueued_at        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    scheduled_at       TIMESTAMP    NULL,
    started_at         TIMESTAMP    NULL,
    completed_at       TIMESTAMP    NULL,
    failure_reason     TEXT         NULL,
    exception_type     VARCHAR(500) NULL,
    exception_detail   TEXT         NULL,
    payload            TEXT         NULL,        -- JSON (JSONB on PostgreSQL)
    output             TEXT         NULL         -- JSON (JSONB on PostgreSQL)
);
```

---

### Queue operations reference

#### Start / End Run

```csharp
// Register a bot session — creates an audit record
var run = await queue.StartRunAsync("sap-invoices", botId: "sap-bot-1");

// ... process items ...

// Mark session complete
await queue.EndRunAsync(run.RunId);
```

#### Populate transactions

```csharp
// Bulk populate (idempotent — items with same Reference are skipped)
await queue.PopulateAsync("sap-invoices", new[]
{
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-001", Priority = 10, Payload = new() { ["amount"] = 5000,  ["vendor"] = "ACME" } },
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-002", Priority = 5,  Payload = new() { ["amount"] = 1200,  ["vendor"] = "Globex" } },
    new WorkItem { QueueId = "sap-invoices", Reference = "INV-003", Priority = 20, Tag = "urgent", Payload = new() { ["amount"] = 99000, ["vendor"] = "Initech" } }
});

// Single item
await queue.EnqueueAsync(new WorkItem
{
    QueueId     = "sap-invoices",
    Reference   = "INV-004",
    Priority    = 1,
    ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),  // delayed start
    Payload     = new Dictionary<string, object> { ["amount"] = 300 }
});
```

#### Assign transactions to a queue / bot

```csharp
// Pre-assign all pending items in a queue to a specific bot
// Items already assigned to another bot are untouched
await queue.AssignQueueToBotAsync("sap-invoices", botId: "sap-bot-1");
```

#### Get next item

```csharp
// Next by priority (any bot)
var item = await queue.GetNextItemAsync("sap-invoices");

// Next assigned to a specific bot
var item = await queue.GetNextItemAsync("sap-invoices", botId: "sap-bot-1");

// Next with a specific tag
var item = await queue.GetNextItemAsync("sap-invoices", tag: "urgent");

// Specific item by reference
var item = await queue.GetNextItemAsync("sap-invoices", reference: "INV-001");

if (item == null)
    Console.WriteLine("Queue empty or no matching items");
else
    Console.WriteLine($"Processing {item.Reference} (attempt {item.AttemptCount})");
```

#### Mark completed / failed

```csharp
// Success — store output data alongside the item
await queue.MarkCompletedAsync(item.ItemId, output: new Dictionary<string, object>
{
    ["sapDocument"] = "5000234",
    ["postedAt"]    = DateTimeOffset.UtcNow
});

// Business failure — re-queued if attempts remaining, dead-lettered if maxAttempts reached
// Precedence: item-level maxAttempts > BotWorker.maxAttempts > global default (3)
await queue.MarkFailedAsync(
    item.ItemId,
    reason:          "Vendor not found in SAP master data",
    exceptionType:   "BusinessException",
    exceptionDetail: null,
    maxAttempts:     3);

// System exception — pass full exception detail
catch (Exception ex)
{
    await queue.MarkFailedAsync(
        item.ItemId,
        reason:          ex.Message,
        exceptionType:   ex.GetType().Name,
        exceptionDetail: ex.ToString(),
        maxAttempts:     3);
}
```

#### Get transaction report

```csharp
var report = await queue.GetReportAsync("sap-invoices");

Console.WriteLine($"Queue:       {report.QueueId}");
Console.WriteLine($"Total:       {report.TotalItems}");
Console.WriteLine($"Pending:     {report.Pending}");
Console.WriteLine($"In Progress: {report.InProgress}");
Console.WriteLine($"Completed:   {report.Completed}");
Console.WriteLine($"Failed:      {report.Failed}");
Console.WriteLine($"Dead Letter: {report.DeadLetter}");
Console.WriteLine($"Success Rate:{report.SuccessRate}%");
Console.WriteLine($"Avg Time:    {report.AverageProcessingTime.TotalSeconds:F1}s");
Console.WriteLine($"Total Time:  {report.TotalElapsedTime?.TotalMinutes:F1}min");
Console.WriteLine($"Runs:        {report.Runs.Count}");

Console.WriteLine("Failures by exception:");
foreach (var (exType, count) in report.FailuresByExceptionType)
    Console.WriteLine($"  {exType}: {count}");

Console.WriteLine("Failed items:");
foreach (var failed in report.FailedItems)
    Console.WriteLine($"  {failed.Reference} — {failed.FailureReason}");
```

### WorkItem status lifecycle

```
        Populate / Enqueue
               │
           [Pending]
               │
        GetNextItemAsync
               │
          [InProgress]
          ┌────┴────┐
          │         │
    MarkCompleted  MarkFailed
          │         │
     [Completed]   attempts < max → [Pending]  (re-queued)
                   attempts >= max → [DeadLetter]
```

---

## BotWorker — Automated Queue Processing

`BotWorker` wraps an `AgentRuntime` and a queue into a continuously running bot. It handles run tracking, concurrent processing, retry, and dead-letter automatically.

```csharp
using Valaiorp.Runtime;
using Valaiorp.Core.Enums;

// 1. Define shared queue (same instance / same SQL table on all machines)
IWorkQueue sharedQueue = new MyQueue();

// 2. Build bot
await using var bot = RuntimeBuilder.BuildBot(
    queueId:        "sap-invoices",
    botId:          "sap-bot",
    config:         new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile(),
    queue:          sharedQueue,
    maxConcurrency: 8,    // parallel items per machine — tools must be stateless or use scoped/thread-local state; avoid shared mutable fields
    maxAttempts:    3,    // before dead-letter
    configureModules: m =>
    {
        m.Register(new SapLoginModule());
        m.Register(new SapPostModule());
    });

// 3. Register your planner
bot.GetService<PlannerOrchestrator>()
   .RegisterPlanner(new SapInvoicePlanner(), setAsDefault: true);

// 4. Start (non-blocking — registers a QueueRun, begins polling)
await bot.StartAsync();

// 5. Monitor
Console.WriteLine($"Pending:     {await bot.GetPendingCountAsync()}");
Console.WriteLine($"In Progress: {await bot.GetInProgressCountAsync()}");
Console.WriteLine($"Dead Letter: {await bot.GetDeadLetterCountAsync()}");

var report = await bot.GetReportAsync();
Console.WriteLine($"Success rate: {report.SuccessRate}%");

// 6. Graceful shutdown — drains in-flight, ends QueueRun
await bot.StopAsync();
// (or: await bot.DisposeAsync() — also disposes the AgentRuntime)
```

### Custom context factory

By default `BotWorker` creates an `ExecutionContext` with `SessionId = item.QueueId` and `Metadata = item.Payload`. Override to enrich the context with secrets, config, etc.:

```csharp
await using var bot = RuntimeBuilder.BuildBot(
    queueId: "sap-invoices",
    botId:   "sap-bot",
    config:  config,
    queue:   sharedQueue,
    contextFactory: item => new MyExecutionContext
    {
        UserId   = "sap-service-account",
        Metadata = new Dictionary<string, object>(item.Payload)
        {
            ["server"]   = Environment.GetEnvironmentVariable("SAP_SERVER")!,
            ["password"] = SecretsManager.Get("SAP_PASS")
        }
    });
```

### Multi-machine deployment

Deploy the same binary on N machines. All bots point at the same `SqlWorkQueue` — no other coordination needed.

```
Machine A:  BuildBot("sap-invoices", "sap-bot", queue: new MyQueue())  →  StartAsync()
Machine B:  BuildBot("sap-invoices", "sap-bot", queue: new MyQueue())  →  StartAsync()
Machine C:  BuildBot("sap-invoices", "sap-bot", queue: new MyQueue())  →  StartAsync()
                             ↕             ↕             ↕
                          Same SQL table — SELECT FOR UPDATE SKIP LOCKED
```

---

## Variable Binding Between Steps

Use `${StepName.Results.FieldName}` in any `PlanStep.Inputs` value — resolved at runtime from the previous step's output before each step executes.

```csharp
new Plan
{
    Steps = new[]
    {
        new PlanStep
        {
            Name   = "ReadConfig",
            ToolId = "json-tool",
            Inputs = new Dictionary<string, object>
            {
                ["operation"] = "read",
                ["filePath"]  = "config.json"
            }
        },
        new PlanStep
        {
            Name   = "CallApi",
            ToolId = "api-tool",
            Inputs = new Dictionary<string, object>
            {
                ["method"] = "GET",
                ["url"]    = "${ReadConfig.Results.endpoint}"  // ← resolved from step 1
            }
        },
        new PlanStep
        {
            Name   = "WriteResult",
            ToolId = "excel-tool",
            Inputs = new Dictionary<string, object>
            {
                ["operation"] = "write",
                ["filePath"]  = "output.xlsx",
                ["data"]      = "${CallApi.Results.body}"  // ← resolved from step 2
            }
        }
    }
}
```

Syntax: `${<StepName>.Results.<dot.path>}` — unresolved references emit a warning log entry (step name + missing key) and are left as-is in the input. Set `ValaiorpConfig.FailOnUnresolvedBindings = true` to treat them as hard errors instead.

---

## Built-in Tools (BasicTools)

`RuntimeBuilder.Build()` automatically calls `BasicToolsRegistry.RegisterAll()` — all built-in tools are available without any manual registration step. If you build the DI container directly without `RuntimeBuilder`, call it yourself:

```csharp
using Valaiorp.BasicTools.Registries;
BasicToolsRegistry.RegisterAll(provider.GetRequiredService<ToolRegistry>());
```

All tools accept `IReadOnlyDictionary<string, object>` parameters.

---

### `json-tool` — JSON files

| Parameter | Values |
|---|---|
| `operation` | `read` \| `write` |
| `filePath` | string |
| `content` | JSON string (write only) |

```csharp
{ ["operation"] = "read",  ["filePath"] = "data.json" }
{ ["operation"] = "write", ["filePath"] = "data.json", ["content"] = "{\"key\":1}" }
```

---

### `jsonl-tool` — JSON Lines

Same parameters as `json-tool`.

---

### `jsonc-tool` — JSON with Comments

Same parameters. Reads with comments preserved; validates by stripping comments before parse.

```csharp
{ ["operation"] = "read",  ["filePath"] = "settings.jsonc" }
{ ["operation"] = "write", ["filePath"] = "settings.jsonc", ["content"] = "{ /* cfg */ \"key\": 1 }" }
```

---

### `txt-tool` — Plain text

| Parameter | Values |
|---|---|
| `operation` | `read` \| `write` \| `append` |
| `filePath` | string |
| `content` | string |

---

### `csv-tool` / `tsv-tool` / `psv-tool` — Delimited files

| Parameter | Values |
|---|---|
| `operation` | `read` \| `write` |
| `filePath` | string |
| `content` | delimited text (write only) |

Read returns `{ rows: [[...]], headers: [...] }`.

---

### `xml-tool` — XML files

Same `read` / `write` parameters. Read returns `{ content: "...", elementCount: N }`.

---

### `excel-tool` — XLSX (built-in lightweight Open XML parser; no extra NuGet required for common operations)

| Parameter | Values | Notes |
|---|---|---|
| `operation` | `read` \| `write` \| `getsheets` \| `readcell` \| `writecell` | |
| `filePath` | string | |
| `sheet` | string | Optional — defaults to first sheet |
| `data` | 2D JSON array string | Required for `write` |
| `cell` | `A1`, `B3` … | Required for `readcell` / `writecell` |
| `value` | string | Required for `writecell` |

```csharp
{ ["operation"] = "read",      ["filePath"] = "report.xlsx" }
{ ["operation"] = "getsheets", ["filePath"] = "report.xlsx" }
{ ["operation"] = "read",      ["filePath"] = "report.xlsx", ["sheet"] = "Q3" }
{ ["operation"] = "write",     ["filePath"] = "out.xlsx", ["data"] = "[[\"Name\",\"Score\"],[\"Alice\",95]]" }
{ ["operation"] = "readcell",  ["filePath"] = "report.xlsx", ["sheet"] = "Q3", ["cell"] = "B2" }
{ ["operation"] = "writecell", ["filePath"] = "report.xlsx", ["sheet"] = "Q3", ["cell"] = "B2", ["value"] = "100" }
```

---

### `word-tool` — DOCX (built-in lightweight Open XML parser; no extra NuGet required for common operations)

| Parameter | Values | Notes |
|---|---|---|
| `operation` | `read` \| `write` \| `append` \| `addheading` \| `addtable` | |
| `filePath` | string | |
| `content` | string | Text / heading text |
| `level` | `1` \| `2` \| `3` | Heading level (default 1) |
| `data` | 2D JSON array string | Required for `addtable` |

```csharp
{ ["operation"] = "read",       ["filePath"] = "report.docx" }
{ ["operation"] = "write",      ["filePath"] = "report.docx", ["content"] = "Line 1\nLine 2" }
{ ["operation"] = "append",     ["filePath"] = "report.docx", ["content"] = "New paragraph." }
{ ["operation"] = "addheading", ["filePath"] = "report.docx", ["content"] = "Section 1", ["level"] = 2 }
{ ["operation"] = "addtable",   ["filePath"] = "report.docx", ["data"] = "[[\"Name\",\"Score\"],[\"Alice\",\"95\"]]" }
```

---

### `folder-tool` — Folder operations

| Parameter | Values |
|---|---|
| `action` | `create` \| `delete` \| `list` \| `copy` \| `move` \| `exists` |
| `path` | Source path |
| `destPath` | Destination (copy/move) |
| `recursive` | `true` \| `false` |
| `pattern` | Glob pattern for `list` |

```csharp
{ ["action"] = "list",   ["path"] = @"C:\Data", ["pattern"] = "*.json" }
{ ["action"] = "delete", ["path"] = @"C:\Temp\old", ["recursive"] = "true" }
{ ["action"] = "copy",   ["path"] = @"C:\Source", ["destPath"] = @"C:\Backup" }
```

---

### `api-tool` — HTTP

| Parameter | Values |
|---|---|
| `method` | `GET` \| `POST` \| `PUT` \| `DELETE` \| `PATCH` |
| `url` | string |
| `body` | JSON string |
| `headers` | JSON object string |

```csharp
{ ["method"] = "GET",  ["url"] = "https://api.example.com/items" }
{ ["method"] = "POST", ["url"] = "https://api.example.com/items", ["body"] = "{\"name\":\"Widget\"}", ["headers"] = "{\"Authorization\":\"Bearer token\"}" }
```

---

### Windows UIAutomation Tool (`net10.0-windows` only)

Tool ID: `windows-ui-automation`. Uses Windows UI Automation (UIA) — works on Win32, WPF, WinForms apps and browser address bars (Edge, Chrome, Firefox).

Input format (pipe-delimited via `input` key): `Action|param1|param2`

| Action | Format | Example |
|---|---|---|
| `FindWindow` | `FindWindow\|windowName` | `FindWindow\|SAP Logon` |
| `Navigate` | `Navigate\|url[\|windowName]` | `Navigate\|https://example.com` |
| `ClickText` | `ClickText\|elementName` | `ClickText\|Submit` |
| `ClickButton` | `ClickButton\|buttonText` | `ClickButton\|OK` |
| `ClickElement` | `ClickElement\|name[\|automationId]` | `ClickElement\|Login\|btnLogin` |
| `GetText` | `GetText\|elementName` | `GetText\|txtResult` |
| `SetText` | `SetText\|elementName\|value` | `SetText\|txtUsername\|admin` |
| `GetTableContent` | `GetTableContent\|tableName` | `GetTableContent\|OrderGrid` |
| `Screenshot` | `Screenshot[\|filePath]` | `Screenshot\|C:\screen.png` |
| `WaitForElement` | `WaitForElement\|name[\|timeoutMs]` | `WaitForElement\|Loading...\|3000` |
| `SelectOption` | `SelectOption\|elementName\|value` | `SelectOption\|cboCountry\|Canada` |
| `SendKeys` | `SendKeys\|keys` | `SendKeys\|Enter` |
| `GetAttribute` | `GetAttribute\|elementName\|attr` | `GetAttribute\|btnOK\|automationid` |

Supported `GetAttribute` names: `name`, `automationid`, `controltype`, `isenabled`, `isvisible`, `helptext`, `value`, `classname`, `processid`.

`SendKeys` recognises: `Enter`, `Tab`, `Esc`, `Backspace`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, `Up`, `Down`, `Left`, `Right`, `F1`–`F12`, or any printable character string.

```csharp
// Pipe-delimited via "input" parameter
{ ["input"] = "SetText|txtUsername|admin" }
{ ["input"] = "ClickButton|Login" }
{ ["input"] = "WaitForElement|Welcome|5000" }
{ ["input"] = "Screenshot|C:\\Reports\\before.png" }
```

---

### Playwright Browser Tool (`PLAYWRIGHT_ENABLED` define)

Tool ID: `playwright-ui-automation`. Requires `Microsoft.Playwright` NuGet package — uncomment the `PackageReference` in [BasicTools/Valaiorp.BasicTools.csproj](BasicTools/Valaiorp.BasicTools.csproj) and add `PLAYWRIGHT_ENABLED` to your compile constants. Launches a Chromium browser (non-headless by default).

Input format (pipe-delimited via `input` key): `Action|param1|param2`

| Action | Format | Example |
|---|---|---|
| `FindWindow` | `FindWindow\|pageTitle` | `FindWindow\|GitHub` |
| `Navigate` | `Navigate\|url` | `Navigate\|https://example.com` |
| `ClickText` | `ClickText\|text` | `ClickText\|Submit` |
| `ClickButton` | `ClickButton\|buttonText` | `ClickButton\|Login` |
| `ClickElement` | `ClickElement\|cssSelector` | `ClickElement\|#submit-btn` |
| `GetText` | `GetText\|cssSelector` | `GetText\|.result` |
| `SetText` | `SetText\|cssSelector\|value` | `SetText\|#username\|admin` |
| `GetTableContent` | `GetTableContent\|cssSelector` | `GetTableContent\|table.data` |
| `Screenshot` | `Screenshot[\|filePath]` | `Screenshot\|output.png` |
| `WaitForElement` | `WaitForElement\|text[\|timeoutMs]` | `WaitForElement\|Loading...\|3000` |
| `SelectOption` | `SelectOption\|label\|value` | `SelectOption\|Country\|Canada` |
| `SendKeys` | `SendKeys\|keys` | `SendKeys\|Enter` |
| `GetAttribute` | `GetAttribute\|cssSelector\|attr` | `GetAttribute\|#link\|href` |
| `EvaluateJs` | `EvaluateJs\|script` | `EvaluateJs\|document.title` |

```csharp
{ ["input"] = "Navigate|https://myapp.internal" }
{ ["input"] = "SetText|#username|admin" }
{ ["input"] = "ClickButton|Sign In" }
{ ["input"] = "WaitForElement|Dashboard|5000" }
{ ["input"] = "Screenshot|C:\\Reports\\after-login.png" }
{ ["input"] = "EvaluateJs|document.querySelector('#token').value" }
```

---

## Creating a Custom Tool

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Core.Enums;
using Valaiorp.Tools.Contracts;
using Valaiorp.Tools.Helpers;   // GetString, GetInt, GetBool, Get<T>

public sealed class DatabaseQueryTool : ITool
{
    public string Id          => "db-query";
    public string Name        => "Database Query";
    public string Description => "Executes SQL. Parameters: query, connectionString.";
    public ToolType Type      => ToolType.Custom;
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();

    public async Task<ToolResult> ExecuteAsync(
        IExecutionContext context,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct = default)
    {
        try
        {
            var query = parameters.GetString("query");
            var cs    = parameters.GetString("connectionString");

            if (string.IsNullOrWhiteSpace(query))
                return ToolResult.BadRequest(new { Message = "'query' is required." });

            var rows = await MyDatabase.QueryAsync(cs, query, ct);
            return ToolResult.Ok(new { rows, count = rows.Count });
        }
        catch (Exception ex) { return ToolResult.Error(ex); }
    }
}

// Register
runtime.GetService<ToolRegistry>().Register(new DatabaseQueryTool());
```

### ToolParameters helpers

```csharp
parameters.GetString("key", fallback: "default")
parameters.GetInt("limit", fallback: 100)
parameters.GetBool("verbose", fallback: false)
parameters.Get<MyType>("data")   // JSON-deserialized
```

---

## Creating a Custom Module

Modules are reusable multi-step sequences. `BaseModule` runs tools sequentially; override `ExecuteAsync` for branching or parallel steps.

```csharp
using Valaiorp.Modules;

public sealed class SapLoginModule : BaseModule
{
    private readonly ITool[] _tools;

    public SapLoginModule(ToolRegistry tools)
    {
        _tools = new[] { tools.Get("ui-open-sap"), tools.Get("ui-set-server"),
                         tools.Get("ui-set-user"),  tools.Get("ui-set-pass"), tools.Get("ui-click-ok") };
    }

    public override string Id          => "sap-login";
    public override string Name        => "SAP Login";
    public override string Description => "Opens SAP and logs in. Parameters: server, username, password.";

    public override IReadOnlyDictionary<string, ParameterDefinition> Parameters => new Dictionary<string, ParameterDefinition>
    {
        ["server"]   = new() { Name = "server",   Type = "string", Required = true, Description = "SAP server ID"  },
        ["username"] = new() { Name = "username", Type = "string", Required = true, Description = "SAP username"   },
        ["password"] = new() { Name = "password", Type = "string", Required = true, Description = "SAP password"   }
    };

    public override IReadOnlyCollection<ITool> Tools => _tools;

    protected override IReadOnlyDictionary<string, object> BuildToolParameters(
        ITool tool, IReadOnlyDictionary<string, object> p)
        => tool.Id switch
        {
            "ui-set-server"  => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtServer",   ["value"] = p["server"] },
            "ui-set-user"    => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtUsername", ["value"] = p["username"] },
            "ui-set-pass"    => new Dictionary<string, object> { ["action"] = "SetText", ["automationId"] = "txtPassword", ["value"] = p["password"] },
            "ui-click-ok"    => new Dictionary<string, object> { ["action"] = "ClickButton", ["element"] = "OK" },
            _                => p
        };
}

// Register
runtime.GetService<ModuleRegistry>().Register(new SapLoginModule(runtime.GetService<ToolRegistry>()));
```

---

## Creating a Custom Planner

```csharp
using Valaiorp.Planner.Contracts;
using Valaiorp.Planner.Models;

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
                new PlanStep
                {
                    Name   = "Login",
                    ToolId = "sap-login",
                    Inputs = new Dictionary<string, object>
                    {
                        ["server"]   = context.Metadata["server"],
                        ["username"] = "svc_bot",
                        ["password"] = Environment.GetEnvironmentVariable("SAP_PASS")!
                    }
                },
                new PlanStep
                {
                    Name   = "PostInvoice",
                    ToolId = "sap-post",
                    Inputs = new Dictionary<string, object>
                    {
                        ["invoiceId"]  = context.Metadata["reference"],
                        ["amount"]     = context.Metadata["amount"],
                        ["costCenter"] = "CC-100"
                    }
                },
                new PlanStep
                {
                    Name   = "LogResult",
                    ToolId = "excel-tool",
                    Inputs = new Dictionary<string, object>
                    {
                        ["operation"] = "append",
                        ["filePath"]  = @"C:\Reports\log.txt",
                        ["content"]   = "${PostInvoice.Results.documentNumber}"
                    }
                },
                new PlanStep { Name = "Logout", ToolId = "sap-logout", Inputs = new Dictionary<string, object>() }
            }
        });
}

// Register
runtime.GetService<PlannerOrchestrator>()
       .RegisterPlanner(new SapInvoicePlanner(), setAsDefault: true);
```

---

## Modules in Plans

Once registered, a module is treated identically to a tool in a plan — just use its ID as `ToolId`:

```csharp
new PlanStep
{
    Name   = "Login",
    ToolId = "sap-login",                    // module ID — resolved automatically
    Inputs = new Dictionary<string, object>
    {
        ["server"]   = "Z003",
        ["username"] = "jsmith",
        ["password"] = "${Creds.Results.sapPassword}"
    }
}
```

---

## Manual Plans (PlannerType.Manual)

Supply a complete plan as a JSON file, JSON string, or `Plan` object — no LLM and no code-authored planner involved. Useful for:
- Developer testing without an API key
- Replaying a logged plan from a previous run
- Accepting a plan generated externally (e.g. an LLM outside the runtime)
- Integration tests with stable, file-based plans

### Usage

```csharp
using Valaiorp.Planner.Planners;

// From a JSON file
var planner = new ManualPlanner("C:\\Plans\\sap-invoice.plan.json", isFilePath: true);

// From a JSON string (e.g. returned by an external LLM call)
var planner = new ManualPlanner(planJson);

// From a code-built Plan object
var planner = new ManualPlanner(new Plan { Steps = new[] { ... } });

// Register as default
runtime.GetService<PlannerOrchestrator>()
       .RegisterPlanner(planner, setAsDefault: true);

await runtime.ExecuteAsync(new MyExecutionContext());
```

### Plan JSON schema (available in SDK samples repo as `Planner/plan.schema.json`)

| Field | Type | Required | Description |
|---|---|---|---|
| `determinism` | `string` | No | `deterministic` \| `semiDeterministic` \| `nonDeterministic` — default `deterministic` |
| `steps` | `PlanStep[]` | **Yes** | Ordered list of steps |

**PlanStep fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | **Yes** | Unique step name — used as the source in `${Name.Results.Field}` bindings |
| `toolId` | `string` | No | Tool or module ID to execute |
| `agentId` | `string` | No | Delegate to a named agent instead of a local tool |
| `mode` | `string` | No | `sequential` \| `parallel` \| `hybrid` — default `sequential` |
| `priority` | `integer` | No | Higher = earlier in parallel batches |
| `inputs` | `object` | No | Key-value parameters. Values may be strings, numbers, booleans, or `${StepName.Results.Field}` references |
| `description` | `string` | No | Human-readable step description |
| `subSteps` | `PlanStep[]` | No | Nested steps for hierarchical plans |
| `metadata` | `object` | No | Arbitrary key-value for tooling or UI |

### Sample plan (available in SDK samples repo as `Planner/plan.sample.json`)

```json
{
  "$schema": "./plan.schema.json",
  "determinism": "deterministic",
  "steps": [
    {
      "name": "ReadConfig",
      "toolId": "json-tool",
      "mode": "sequential",
      "inputs": {
        "operation": "read",
        "filePath": "C:\\BotConfig\\config.json"
      }
    },
    {
      "name": "FetchOrders",
      "toolId": "api-tool",
      "inputs": {
        "method": "GET",
        "url":    "${ReadConfig.Results.endpoint}",
        "headers": "{\"Authorization\": \"Bearer ${ReadConfig.Results.apiKey}\"}"
      }
    },
    {
      "name": "SapLogin",
      "toolId": "sap-login",
      "inputs": {
        "server":   "Z003",
        "username": "svc_bot",
        "password": "{{SAP_PASS}}"
      }
    },
    {
      "name": "PostInvoices",
      "toolId": "sap-post",
      "inputs": {
        "data":       "${FetchOrders.Results.body}",
        "costCenter": "CC-100"
      }
    },
    {
      "name": "ExportReport",
      "toolId": "excel-tool",
      "inputs": {
        "operation": "write",
        "filePath":  "C:\\Reports\\postings.xlsx",
        "data":      "${PostInvoices.Results.rows}"
      }
    },
    {
      "name": "SapLogout",
      "toolId": "sap-logout",
      "inputs": {}
    }
  ]
}
```

### Variable binding in plans

Two expansion syntaxes are available in plan JSON:

| Syntax | When expanded | Source |
|--------|--------------|--------|
| `${StepName.Results.Field}` | At runtime, just before each step executes | Previous step output |
| `{{ENV_VAR_NAME}}` | Before the plan is loaded, during pre-execution template expansion | Environment variables |

`${…}` dot-paths descend into nested objects: `${Step.Results.data.items}`. Unresolved `${…}` references emit a warning and are left as-is; set `ValaiorpConfig.FailOnUnresolvedBindings = true` to error instead. `{{…}}` references that have no matching environment variable are left as-is without warning.

---

## LLM Providers

A single `GenericLlmClient` handles all providers via JSON profiles. Seven profiles are built in.

```csharp
using Valaiorp.Configuration.Models;
using Valaiorp.LlmProviders.DependencyInjection;

builder.Services.AddLlmClient(new LlmConfig
{
    Provider     = "anthropic",   // built-in: "anthropic" | "openai" | "ollama" | "gemini" | "mistral" | "cohere" | "nvidia"
    ModelId      = "claude-sonnet-4-6",
    MaxTokens    = 4096,
    Temperature  = 0.7f,
    ApiKeyEnvVar = "ANTHROPIC_API_KEY"    // env var name — resolved at runtime
});
```

API key resolution order: `ApiKey` (literal) → `ApiKeyEnvVar` → `ApiKeyFile` → `{PROVIDER}_API_KEY` env var.

### Adding a custom provider (no C# required)

Create a JSON profile and call `AddLlmClientFromProfile`:

```csharp
builder.Services.AddLlmClientFromProfile("my-provider.json", new LlmConfig
{
    Provider     = "myprovider",
    ModelId      = "my-model-id",
    ApiKeyEnvVar = "MYPROVIDER_API_KEY"
});
```

The profile file mirrors the built-in profiles (see `LlmProviders/Profiles/*.json` for examples): `defaultBaseUrl`, `endpoint`, `authHeader`, `requestBody`, `responseMapping`.

### Calling the LLM directly

```csharp
var llm = runtime.GetService<ILlmClient>();

var response = await llm.CompleteAsync(new PromptContext
{
    SystemPrompt = "You are a helpful assistant.",
    UserPrompt   = "Summarise the quarterly report.",
    RagContext   = new[] { "Q3 revenue was $4.2M..." }
});

Console.WriteLine(response.IsSuccess ? response.Content : response.Error);
```

---

## Multi-Agent

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Core.Enums;

public sealed class ResearchAgent : IAgent
{
    public string AgentId => "research";
    public AgentRole Role => AgentRole.Specialist;
    public IReadOnlyList<string> Capabilities => new[] { "search", "summarise" };

    public async Task<AgentResult> RunAsync(AgentMessage message, CancellationToken ct = default)
    {
        var answer = /* call LLM or tools */ "";
        return AgentResult.Ok(AgentId, message.ConversationId, answer);
    }
}

var registry = runtime.GetService<IAgentRegistry>();
registry.Register(new OrchestratorAgent(), setAsDefaultOrchestrator: true);
registry.Register(new ResearchAgent());

// Run
var orchestrator = runtime.GetService<MultiAgentOrchestrator>();
var result = await orchestrator.RunAsync(new AgentMessage
{
    ConversationId = Guid.NewGuid().ToString("N"),
    Prompt = new PromptContext { UserPrompt = "Research AI governance trends." }
});
```

---

## Policy Rules

```csharp
using Valaiorp.Policy.Models;

public sealed class BlockedUserPolicy : PolicyRule
{
    public override Task<PolicyResult> EvaluatePreExecutionAsync(
        IExecutionContext context, CancellationToken ct = default)
        => Task.FromResult(context.UserId == "banned"
            ? PolicyResult.Denied("User is blocked.")
            : PolicyResult.Allowed());

    public override Task<PolicyResult> EvaluatePostExecutionAsync(
        IExecutionResult result, CancellationToken ct = default)
        => Task.FromResult(PolicyResult.Allowed());
}

runtime.GetService<IPolicyEngine>().AddRule(new BlockedUserPolicy());
```

---

## Guardrails

Guardrails are content and action safety checks that run **at the content level** — scanning what goes in and what comes out, before any policy or LLM call is made. They are distinct from Policy Rules: policy rules decide whether a context should execute at all; guardrails decide whether specific content is safe to send or receive.

### How guardrails fit in the execution flow

```
User Input
    │
[Input Guardrails]   ← PII redaction · prompt injection · banned keywords · length
    │
Pre-Policy Check
    │
Plan → Execute Steps
    │
[Output Guardrails]  ← content length · data classification
    │
Post-Policy Check
    │
Commit / Return
```

### Configuration (auto-wired by RuntimeBuilder)

Set `ValaiorpConfig.Guardrails` and the runtime wires all enabled guardrails automatically:

```csharp
var config = new ValaiorpConfig
{
    WorkflowType = WorkflowType.AiAgent,
    Guardrails = new GuardrailConfig
    {
        EnablePromptInjectionDetection = true,    // default: true
        EnablePiiRedaction             = true,    // default: false
        EnableBannedKeywords           = true,
        BannedKeywords                 = new[] { "DROP TABLE", "rm -rf", "format c:" },
        MaxInputLengthChars            = 16_000,  // 0 = disabled
        MaxOutputLengthChars           = 32_000,
        DeniedToolIds                  = new[] { "shell-tool", "raw-sql-tool" },
        AllowedToolIds                 = null,    // null = all tools allowed (subject to DeniedToolIds)
        EnableDataClassification       = true
    }
}.ApplyProfile();
```

### Standalone DI registration

Use `AddGuardrails()` when wiring without `RuntimeBuilder`:

```csharp
using Valaiorp.Guardrails.DependencyInjection;

services.AddGuardrails(opts =>
{
    opts.EnablePiiRedaction             = true;
    opts.EnablePromptInjectionDetection = true;
    opts.EnableBannedKeywords           = true;
    opts.BannedKeywords                 = new[] { "DROP TABLE", "secret" };
    opts.MaxInputLengthChars            = 16_000;
    opts.DeniedToolIds                  = new[] { "shell-tool" };
    opts.EnableDataClassification       = true;
});
```

### Built-in guardrails

| Guardrail | ID | Scope | Default Action | What it checks |
|---|---|---|---|---|
| `PiiGuardrail` | `pii-guardrail` | All | **Redact** | Email · phone · SSN · credit card · IP address — replaced with `[EMAIL]` `[PHONE]` `[SSN]` `[CARD]` `[IP]` |
| `BannedKeywordsGuardrail` | `banned-keywords-guardrail` | All | **Block** | Case-insensitive match against configured keyword list |
| `PromptInjectionGuardrail` | `prompt-injection-guardrail` | Input | **Block** | 12 injection patterns: "ignore instructions", "you are now", jailbreak, DAN, etc. |
| `ContentLengthGuardrail` | `content-length-input-guardrail` | Input | **Block** | Content exceeds `MaxInputLengthChars` |
| `ContentLengthGuardrail` | `content-length-output-guardrail` | Output | **Block** | Content exceeds `MaxOutputLengthChars` |
| `ToolScopeGuardrail` | `tool-scope-guardrail` | Tool | **Block** | Tool ID not in `AllowedToolIds`, or is in `DeniedToolIds` |
| `DataClassificationGuardrail` | `data-classification-guardrail` | All | **Warn** | Tags content as Public / Internal / Confidential / Restricted — never blocks, adds metadata |

### Pipeline evaluation rules

The pipeline runs each guardrail in registration order:

1. **Block** — the first violation stops the pipeline immediately. Execution is rejected and an error is returned.
2. **Escalate** — stops the pipeline and routes to `IEscalationService.RequestApprovalAsync`. Execution continues only if the human approves; otherwise it is rejected with the escalation reason.
3. **Redact** — the sanitised `SafeContent` replaces the original and is forwarded to the next guardrail. Execution continues with clean content.
4. **Warn** — logged in the result metadata. Execution continues unchanged.

Example: a `GuardrailResult.Escalate(...)` result automatically calls the registered `IApprovalProvider` (see [Escalation](#escalation-human-in-the-loop)):

```csharp
// Inside a custom guardrail
return Task.FromResult(GuardrailResult.Escalate(Id,
    "Content references restricted vendor — human review required"));
// → runtime calls IEscalationService.RequestApprovalAsync(context, reason)
// → execution continues only on ApprovalResult.Approved
```

### GuardrailResult shape

```csharp
result.IsAllowed    // false → execution stopped
result.Action       // Block | Redact | Warn | Escalate
result.Reason       // why the guardrail fired
result.SafeContent  // redacted content (non-null when Action = Redact)
result.GuardrailId  // which guardrail fired
result.Metadata     // e.g. DataClassification = "Restricted"
```

### Using IGuardrailPipeline directly

```csharp
using Valaiorp.Guardrails.Contracts;

var pipeline = runtime.GetService<IGuardrailPipeline>();

// Evaluate a user prompt before passing it to the LLM or planner
var inputResult = await pipeline.EvaluateInputAsync(context, userPrompt);
if (!inputResult.IsAllowed)
{
    Console.WriteLine($"Blocked by [{inputResult.GuardrailId}]: {inputResult.Reason}");
    return;
}
// Use SafeContent (PII-redacted) if available, otherwise original
var safePrompt = inputResult.SafeContent ?? userPrompt;

// Evaluate a tool call before it executes
var toolResult = await pipeline.EvaluateToolCallAsync(context, "shell-tool",
    new Dictionary<string, object> { ["command"] = userInput });
if (!toolResult.IsAllowed)
    throw new UnauthorizedAccessException($"Tool blocked: {toolResult.Reason}");

// Evaluate an LLM response before using it
var outputResult = await pipeline.EvaluateOutputAsync(context, llmResponse);
if (!outputResult.IsAllowed)
{
    // Roll back or log — execution should not continue
}
```

### Custom guardrail

```csharp
using Valaiorp.Guardrails.Contracts;
using Valaiorp.Guardrails.Enums;
using Valaiorp.Guardrails.Models;

public sealed class RestrictedVendorGuardrail : IGuardrail
{
    public string Id   { get; } = "restricted-vendor-guardrail";
    public string Name { get; } = "Restricted Vendor Check";
    public GuardrailScope Scope { get; } = GuardrailScope.Input;
    public bool IsEnabled { get; set; } = true;

    private static readonly string[] _restricted = ["AcmeCorp", "Globex", "Initech"];

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
    {
        var content = context.Content ?? string.Empty;
        foreach (var vendor in _restricted)
        {
            if (content.Contains(vendor, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(GuardrailResult.Escalate(Id,
                    $"Content references restricted vendor \"{vendor}\" — human review required"));
        }
        return Task.FromResult(GuardrailResult.Allow(content));
    }
}

// Register via DI options
services.AddGuardrails(opts => opts.AddCustomGuardrail(new RestrictedVendorGuardrail()));

// Or directly on the live pipeline
runtime.GetService<IGuardrailPipeline>().Add(new RestrictedVendorGuardrail());
```

---

## Knowledge / RAG

Implement `IKnowledgeProvider` to wrap any vector store (Qdrant, Pinecone, in-memory, etc.). The example below uses an in-memory list; swap the body of `SearchAsync` for your vector store client.

```csharp
using Valaiorp.Knowledge.Contracts;

public sealed class InMemoryRagProvider : IKnowledgeProvider
{
    public string Id => "rag";
    public string Name => "In-Memory RAG";
    public string Description => "Simple keyword-match knowledge base for testing.";
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    private static readonly string[] _documents =
    [
        "Q3 revenue was $4.2M, up 18% YoY.",
        "The refund policy allows returns within 30 days.",
        "Our SLA guarantees 99.9% uptime for enterprise plans."
    ];

    public Task<IReadOnlyCollection<string>> SearchAsync(
        string query, int maxResults, CancellationToken ct = default)
    {
        // Replace with your vector store client (Qdrant, Pinecone, Azure AI Search, etc.)
        var results = _documents
            .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<string>>(results);
    }
}

runtime.GetService<KnowledgeProviderResolver>()
       .RegisterProvider(new InMemoryRagProvider(), setAsDefault: true);
```

For Qdrant, replace the `SearchAsync` body with:

```csharp
var client = new QdrantClient("localhost");
var hits   = await client.SearchAsync("knowledge", embeddingOf(query), limit: (ulong)maxResults, cancellationToken: ct);
return hits.Select(h => h.Payload["text"].StringValue).ToArray();
```

---

## Escalation (Human-in-the-Loop)

```csharp
using Valaiorp.Escalation.Contracts;

// Example: approval via an HTTP webhook (replace with Slack, Teams, DB, etc.)
public sealed class HttpApprovalProvider(HttpClient http) : IApprovalProvider
{
    public async Task<ApprovalResult> RequestApprovalAsync(
        IExecutionContext context, string action, string? description = null,
        IDictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var payload = new { contextId = context.Id, action, description };
        var response = await http.PostAsJsonAsync(
            Environment.GetEnvironmentVariable("APPROVAL_WEBHOOK_URL"), payload, ct);

        if (!response.IsSuccessStatusCode)
            return ApprovalResult.Rejected(reason: $"Webhook returned {response.StatusCode}");

        var body = await response.Content.ReadFromJsonAsync<ApprovalWebhookResponse>(ct);
        return body?.Approved == true
            ? ApprovalResult.Approved(approverId: body.ApproverId ?? "webhook")
            : ApprovalResult.Rejected(reason: body?.Reason ?? "Rejected by approver");
    }

    private sealed record ApprovalWebhookResponse(bool Approved, string? ApproverId, string? Reason);
}

builder.Services.AddEscalationInterfaces();
builder.Services.AddSingleton<IApprovalProvider, HttpApprovalProvider>();

// Use
var escalation = runtime.GetService<IEscalationService>();
var approval = await escalation.RequestApprovalAsync(context, "delete-record", "Deletes customer 12345");
if (!approval.IsApproved) return;
```

---

## Retry

### Tool-level (automatic)

| Policy | Default |
|---|---|
| `MaxAttemptsRetryPolicy` | 3 attempts |
| `ExponentialBackoffRetryPolicy` | 100 ms → 10 s, doubles each attempt |
| `CircuitBreakerRetryPolicy` | Opens after 5 failures, resets after 30 s |

### Queue-level (automatic via BotWorker)

| Outcome | Action |
|---|---|
| Success | `MarkCompletedAsync` |
| Failure, attempts remaining | `MarkFailedAsync` — item re-queued |
| Failure, max attempts reached | `MarkFailedAsync` — item dead-lettered |

### Replace the tool-level strategy

```csharp
using Valaiorp.Retry.Policies;
using Valaiorp.Retry.Strategies;

builder.Services.AddSingleton<IRetryStrategy>(_ =>
    new RetryStrategy(new MaxAttemptsRetryPolicy(2)));
```

---

## Memory

All three memory stores default to JSONL file-backed implementations (directory: `PersistenceConfig.MemoryDirectory`, default `"memory"`). State survives process restarts out of the box — no database required.

```csharp
var memory = runtime.GetService<MemoryManager>();

// Short-term (session-scoped, JSONL file-backed by default)
await memory.ShortTerm.SetAsync("step", "login", ct);
var step = await memory.ShortTerm.GetAsync<string>("step", ct);

// Long-term (persistent, JSONL file-backed by default)
await memory.LongTerm.StoreLogAsync(new ExecutionLog
{
    ContextId = context.Id,
    StepName  = "PostInvoice",
    Duration  = TimeSpan.FromMilliseconds(820)
}, ct);
var logs = await memory.LongTerm.RetrieveLogsAsync(context.Id, ct);
```

To swap to a different backing store, replace the registered implementations after calling `AddAgenticAIRuntime`:

```csharp
// Redis-backed short-term memory
services.AddSingleton<IShortTermMemory, RedisShortTermMemory>();

// SQL-backed long-term memory
services.AddSingleton<ILongTermMemory, SqlLongTermMemory>();

// Vector-backed long-term memory (semantic search over logs)
services.AddSingleton<ILongTermMemory, VectorLongTermMemory>();
```

---

## Execution Logging

Automatic — three entries written per execution to JSONL files (directory: `PersistenceConfig.LogDirectory`, default `"logs"`). This happens automatically; no configuration required.

| Event | Trigger |
|-------|---------|
| Plan  | After plan creation |
| Step  | After each step completes |
| Run   | After executor finishes |

### Also log to SQL

Call `AddSqlPersistence` after `AddAgenticAIRuntime` to write logs to both JSONL files and SQL simultaneously:

```csharp
using Valaiorp.Runtime.DependencyInjection;

services.AddAgenticAIRuntime(config)
        .AddSqlPersistence(() => new SqlConnection(connectionString));

// Create the SQL schema once at startup (idempotent)
await provider.GetRequiredService<SqlExecutionLogger>().CreateSchemaAsync();
```

Works with any ADO.NET provider — `SqlConnection`, `NpgsqlConnection`, `SqliteConnection`, etc.

---

## ASP.NET Core / Worker Service Integration

```csharp
// Program.cs
builder.Services.AddAgenticAIRuntime(new ValaiorpConfig
{
    WorkflowType = WorkflowType.Irpa
}.ApplyProfile());

public class BotHostedService(AgentRuntime runtime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var result = await runtime.ExecuteAsync(new MyExecutionContext(), ct);
    }
}
```

---

## Requirements

- .NET 10 runtime
- `Microsoft.Extensions.DependencyInjection` (transitive via `Valaiorp.Runtime`)
- Windows only: `windows-ui-automation` tool requires `net10.0-windows` TFM
- Optional: `Microsoft.Playwright` NuGet + `PLAYWRIGHT_ENABLED` compile constant for Playwright browser automation
- Optional: ADO.NET provider NuGet (e.g. `Microsoft.Data.SqlClient`, `Npgsql`, `Microsoft.Data.Sqlite`) for `SqlWorkQueue` and `AddSqlPersistence`
