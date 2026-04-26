# Valaiorp — Usage Guide

Consuming the Valaiorp framework as compiled assemblies in a .NET 10 project.

---

## Required Assemblies

| Assembly | Purpose |
|----------|---------|
| `Valaiorp.Core.dll` | Contracts, enums, base entities — always required |
| `Valaiorp.Configuration.dll` | `ValaiorpConfig` and all config models |
| `Valaiorp.Runtime.dll` | `AgentRuntime`, `RuntimeBuilder`, DI extensions — always required |
| `Valaiorp.Memory.dll` | Short-term, long-term, and conversation memory |
| `Valaiorp.Tools.dll` | `ITool`, `IModule`, registries |
| `Valaiorp.BasicTools.dll` | Built-in file, folder, API, UIAutomation, browser tools |
| `Valaiorp.Policy.dll` | `PolicyRule`, `IPolicyEngine` |
| `Valaiorp.Planner.dll` | `IPlanner`, `Plan`, orchestrator |
| `Valaiorp.Knowledge.dll` | `IKnowledgeProvider`, resolver |
| `Valaiorp.Execution.dll` | Execution engine, workflow builder, transactions |
| `Valaiorp.Observability.dll` | Console logging, tracing, metrics |
| `Valaiorp.Retry.dll` | Retry policies — auto-registered by Runtime |
| `Valaiorp.Logging.dll` | Execution logging (plan, run, step) — auto-registered by Runtime |
| `Valaiorp.LlmProviders.dll` | Anthropic, OpenAI, Ollama clients |
| `Valaiorp.MultiAgent.dll` | Multi-agent registry and orchestration |
| `Valaiorp.Escalation.dll` | Approval, override, and escalation handling |

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
  <!-- add remaining DLLs you need -->
</ItemGroup>
```

---

## Quickstart

### 1. Implement `IExecutionContext`

```csharp
using Valaiorp.Core.Contracts;

public sealed class MyExecutionContext : IExecutionContext
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public string UserId { get; init; } = "anonymous";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    public IReadOnlyCollection<IExecutionStep> Steps { get; init; } = Array.Empty<IExecutionStep>();
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}
```

### 2. Build and run

```csharp
using Valaiorp.Runtime;

// Default config — sequential, 5-minute timeout, 3 retries
await using var runtime = RuntimeBuilder.Build();

var context = new MyExecutionContext { UserId = "user-42" };
var result = await runtime.ExecuteAsync(context);

Console.WriteLine(result.IsSuccess
    ? $"Done in {result.ExecutionTime.TotalSeconds:F2}s"
    : $"Failed: {result.ErrorMessage}");
```

---

## Configuration

### From a JSON file

```csharp
await using var runtime = RuntimeBuilder.BuildFromFile("appsettings.json");
```

`appsettings.json`:

```json
{
  "Execution": {
    "Mode": "Sequential",
    "Timeout": "00:05:00",
    "MaxRetries": 3,
    "EnableCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerResetTime": "00:00:30"
  },
  "Planner": {
    "Type": "Reactive",
    "MaxDepth": 10,
    "MaxBranchingFactor": 5,
    "EnableBacktracking": true,
    "PlanningTimeout": "00:00:30"
  },
  "Knowledge": {
    "EmbeddingModel": "text-embedding-ada-002",
    "EmbeddingDimension": 1536,
    "SimilarityThreshold": 0.75,
    "MaxContextLength": 4096,
    "MaxKnowledgeResults": 5
  },
  "Parallelism": {
    "MaxDegreeOfParallelism": 4,
    "MaxConcurrentExecutions": 10,
    "EnableDynamicScaling": true,
    "MinThreadPoolSize": 4,
    "MaxThreadPoolSize": 50
  },
  "Governance": {
    "EnableAuditLogging": true,
    "EnableMetrics": true,
    "EnableRateLimiting": true,
    "MaxRequestsPerSecond": 100,
    "EnableContentModeration": true,
    "BannedKeywords": []
  },
  "Llm": {
    "Provider": "anthropic",
    "ModelId": "claude-sonnet-4-6",
    "MaxTokens": 4096,
    "Temperature": 0.7,
    "ApiKeyEnvVar": "ANTHROPIC_API_KEY"
  }
}
```

### In code

```csharp
using Valaiorp.Configuration;
using Valaiorp.Core.Enums;

var config = new ValaiorpConfig
{
    Execution = new ExecutionConfig
    {
        Mode = ExecutionMode.Parallel,
        Timeout = TimeSpan.FromMinutes(2),
        MaxRetries = 5
    },
    Parallelism = new ParallelismConfig
    {
        MaxDegreeOfParallelism = 8,
        MaxConcurrentExecutions = 20
    }
};

await using var runtime = RuntimeBuilder.Build(config);
```

---

## Retry Behaviour

Retry is **automatic** — no extra setup required. Every tool call made by `ParallelExecutor` is wrapped in a composite retry strategy.

| Policy | Default | Description |
|--------|---------|-------------|
| `MaxAttemptsRetryPolicy` | 3 attempts | Stops after N total attempts |
| `ExponentialBackoffRetryPolicy` | 100 ms initial, 10 s max, 5 attempts | Doubles delay between attempts |
| `CircuitBreakerRetryPolicy` | 5 failures / 30 s reset | Opens the circuit after repeated failures |

All three are composed with AND logic — a retry only happens when every policy allows it.

### Replacing the retry strategy

```csharp
using Valaiorp.Retry.Contracts;
using Valaiorp.Retry.Policies;
using Valaiorp.Retry.Strategies;

builder.Services.AddAgenticAIRuntime(config);

builder.Services.AddSingleton<IRetryStrategy>(_ =>
    new RetryStrategy(new MaxAttemptsRetryPolicy(2)));
```

---

## Execution Logging

`AgentRuntime` writes three log entries per execution automatically.

| Event | When | Key (LocalExecutionLogger) |
|-------|------|---------------------------|
| **Plan** | After the planner produces a plan | `execution_log_plan_{planId}` |
| **Step** | After all steps complete | `execution_log_step_{stepId}` |
| **Run** | After the executor finishes | `execution_log_run_{executionId}` |

### Reading logs from memory

```csharp
var memory = runtime.GetService<MemoryManager>();
var runLog = await memory.ShortTerm.GetAsync<object>("execution_log_run_<executionId>", ct);
```

### Switching to file-based logging

```csharp
using Valaiorp.Tools.Enhanced.Logging;

builder.Services.AddAgenticAIRuntime(config);
builder.Services.AddSingleton<IExecutionLogger>(
    _ => new ExternalExecutionLogger(logDirectory: "logs"));
```

Each execution produces: `plan_{id}.jsonl`, `run_{id}.jsonl`, and one `step_{id}.jsonl` per step.

---

## Built-in Tools (BasicTools)

All built-in tools use a **pipe-delimited input string**: `action|param1|param2`

Register all built-in tools at once:

```csharp
using Valaiorp.BasicTools;

var toolRegistry = runtime.GetService<ToolRegistry>();
BasicToolsRegistry.RegisterAll(toolRegistry); // registers file, folder, API tools
// UITools and BrowserTools registered conditionally (Windows / PLAYWRIGHT_ENABLED)
```

### File Tools

| Tool | Input format |
|------|-------------|
| `JsonTool` | `read\|filePath` or `write\|filePath\|content` |
| `JsonlTool` | `read\|filePath` or `write\|filePath\|content` |
| `TxtTool` | `read\|filePath` or `write\|filePath\|content` |
| `CsvTool` | `read\|filePath` or `write\|filePath\|content` |
| `TsvTool` | `read\|filePath` or `write\|filePath\|content` |
| `PsvTool` | `read\|filePath` or `write\|filePath\|content` |
| `XmlTool` | `read\|filePath` or `write\|filePath\|content` |

### Folder Tool

```
create|path
delete|path[|recursive]
list|path[|pattern]
copy|src|dest
move|src|dest
exists|path
```

### API Tool (HTTP)

```
GET|https://api.example.com/resource
POST|https://api.example.com/resource|{"key":"value"}
PUT|https://api.example.com/resource|{"key":"value"}|{"Authorization":"Bearer token"}
DELETE|https://api.example.com/resource
PATCH|https://api.example.com/resource|{"key":"value"}
```

### Windows UIAutomation Tool (net10.0-windows only)

```
FindWindow|windowTitle
ClickText|textLabel
ClickButton|buttonName
ClickElement|automationId
GetText|automationId
SetText|automationId|value
GetTableContent|automationId
Navigate|url
```

### Playwright Browser Tool (requires `PLAYWRIGHT_ENABLED`)

Same action format as UIAutomation, backed by Chromium/Firefox/WebKit.

---

## LLM Providers

Wire up an LLM client via DI using `LlmConfig`:

```csharp
using Valaiorp.Configuration.Models;
using Valaiorp.LlmProviders.DependencyInjection;

builder.Services.AddLlmClient(new LlmConfig
{
    Provider    = "anthropic",          // "anthropic" | "openai" | "ollama"
    ModelId     = "claude-sonnet-4-6",
    MaxTokens   = 4096,
    Temperature = 0.7f,
    ApiKeyEnvVar = "ANTHROPIC_API_KEY"  // reads from environment variable
});
```

API key resolution order: `ApiKey` (literal) → `ApiKeyEnvVar` → `ApiKeyFile` → `{PROVIDER}_API_KEY` env var.

### Calling the LLM directly

```csharp
using Valaiorp.Core.Contracts;

var llm = runtime.GetService<ILlmClient>();

var response = await llm.CompleteAsync(new PromptContext
{
    SystemPrompt = "You are a helpful assistant.",
    UserPrompt   = "Summarise the quarterly report.",
    RagContext   = new[] { "Q3 revenue was $4.2M..." },
    Variables    = new Dictionary<string, string> { ["format"] = "bullet points" }
});

Console.WriteLine(response.IsSuccess ? response.Content : response.Error);
Console.WriteLine($"Tokens: {response.InputTokens} in / {response.OutputTokens} out");
```

### Using a custom IApiKeyProvider (Azure Key Vault, AWS Secrets Manager, etc.)

```csharp
using Valaiorp.Core.Contracts;

public sealed class AzureKeyVaultProvider : IApiKeyProvider
{
    public async Task<string?> GetApiKeyAsync(string keyName, CancellationToken ct = default)
    {
        // retrieve from Azure Key Vault
        return await vault.GetSecretAsync(keyName, ct);
    }
}

builder.Services.AddLlmClient(config, new AzureKeyVaultProvider());
```

---

## Multi-Agent

### Register agents

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

public sealed class OrchestratorAgent : IAgent
{
    public string AgentId => "orchestrator";
    public AgentRole Role => AgentRole.Orchestrator;
    public IReadOnlyList<string> Capabilities => Array.Empty<string>();

    public async Task<AgentResult> RunAsync(AgentMessage message, CancellationToken ct = default)
    {
        // Delegate to sub-agents by returning DelegatedMessages
        return AgentResult.Delegate(AgentId, message.ConversationId,
            new AgentMessage
            {
                ConversationId = message.ConversationId,
                ToAgentId      = "research",
                Prompt = new PromptContext { UserPrompt = message.Prompt.UserPrompt }
            });
    }
}

var registry = runtime.GetService<IAgentRegistry>();
registry.Register(new OrchestratorAgent(), setAsDefaultOrchestrator: true);
registry.Register(new ResearchAgent());
```

### Run a multi-agent conversation

```csharp
using Valaiorp.MultiAgent.Orchestration;

var orchestrator = runtime.GetService<MultiAgentOrchestrator>();
orchestrator.MaxRounds = 10; // default 20

var result = await orchestrator.RunAsync(new AgentMessage
{
    ConversationId = Guid.NewGuid().ToString("N"),
    Prompt = new PromptContext
    {
        SystemPrompt = "You are a research coordinator.",
        UserPrompt   = "Find and summarise recent AI governance papers."
    }
});

Console.WriteLine(result.IsSuccess ? result.Output : result.Error);
```

Sub-agents can run **in parallel** by setting `payload["parallel"] = true` on the delegated message. Sequential sub-agents run one after another.

---

## Escalation (Human-in-the-Loop)

The Escalation module provides interfaces for approval, override, and escalation hooks. Implement them in your host application; the framework never calls external systems directly.

### Register your providers

```csharp
using Valaiorp.Escalation.DependencyInjection;

builder.Services.AddEscalationInterfaces();

// Then override the default throw-not-registered stubs with real implementations:
builder.Services.AddSingleton<IApprovalProvider, MyApprovalProvider>();
builder.Services.AddSingleton<IOverrideProvider, MyOverrideProvider>();
builder.Services.AddSingleton<IEscalationHandler, MyEscalationHandler>();
```

### Implement `IApprovalProvider`

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Escalation.Contracts;

public sealed class MyApprovalProvider : IApprovalProvider
{
    public async Task<ApprovalResult> RequestApprovalAsync(
        IExecutionContext context,
        string action,
        string? description = null,
        IDictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        // Send to your approval UI, API, or messaging system
        var approved = await MyApprovalApi.RequestAsync(action, description, ct);
        return approved
            ? ApprovalResult.Approved(approverId: "manager-1")
            : ApprovalResult.Rejected(reason: "Out of policy");
    }
}
```

### Use via `IEscalationService`

```csharp
using Valaiorp.Escalation.Contracts;

var escalation = runtime.GetService<IEscalationService>();

// Request approval before a sensitive action
var approval = await escalation.RequestApprovalAsync(
    context, action: "delete-customer-record",
    description: "Deletes record for customer 12345");

if (!approval.IsApproved)
{
    Console.WriteLine($"Rejected: {approval.Reason}");
    return;
}

// Request a parameter override
var @override = await escalation.RequestOverrideAsync(
    context, action: "send-email",
    overrideReason: "Recipient corrected by supervisor",
    newParameters: new Dictionary<string, object> { ["to"] = "correct@example.com" });

// Escalate on unexpected state
var escalationResult = await escalation.HandleEscalationAsync(
    context, reason: EscalationReason.PolicyViolation,
    description: "Risk score exceeded threshold");
```

---

## Registering a Custom Tool

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Core.Enums;
using Valaiorp.Tools.Contracts;

public sealed class SearchTool : ITool
{
    public string Id => "search";
    public string Name => "Search";
    public string Description => "Searches an external index.";
    public ToolType Type => ToolType.Custom;
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();

    public async Task<ToolResult> ExecuteAsync(
        IExecutionContext context, string input, CancellationToken ct = default)
    {
        var hits = await MySearchClient.QueryAsync(input, ct);
        return ToolResult.Ok(Id, new Dictionary<string, object> { ["hits"] = hits });
    }
}

var toolRegistry = runtime.GetService<ToolRegistry>();
toolRegistry.Register(new SearchTool());
```

---

## Registering a Custom Planner

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Core.Enums;
using Valaiorp.Planner.Contracts;
using Valaiorp.Planner.Models;

public sealed class MyPlanner : IPlanner
{
    public string Id => "my-planner";
    public PlannerType Type => PlannerType.Deliberative;
    public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;

    public Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
    {
        var plan = new Plan
        {
            ContextId = context.Id,
            Steps = new[]
            {
                new PlanStep { Name = "Step1", ToolId = "search", Mode = ExecutionMode.Sequential }
            }
        };
        return Task.FromResult(plan);
    }
}

var orchestrator = runtime.GetService<PlannerOrchestrator>();
orchestrator.RegisterPlanner(new MyPlanner(), setAsDefault: true);
```

---

## Registering a Policy Rule

```csharp
using Valaiorp.Core.Contracts;
using Valaiorp.Policy.Models;

public sealed class BlockedUserPolicy : PolicyRule
{
    public override Task<PolicyResult> EvaluatePreExecutionAsync(
        IExecutionContext context, CancellationToken ct = default)
        => Task.FromResult(
            context.UserId == "banned"
                ? PolicyResult.Denied("User is blocked.")
                : PolicyResult.Allowed());

    public override Task<PolicyResult> EvaluatePostExecutionAsync(
        IExecutionResult result, CancellationToken ct = default)
        => Task.FromResult(PolicyResult.Allowed());
}

var policyEngine = runtime.GetService<IPolicyEngine>();
policyEngine.AddRule(new BlockedUserPolicy());
```

---

## Registering a Knowledge Provider (RAG)

`IKnowledgeProvider` is the single integration point for any RAG backend. Implement it once; the planner and execution engine call it transparently. Developers choose their backend at registration time — the framework has no opinion on which you use.

### Supported backend options

| Backend | How to integrate |
|---------|-----------------|
| Your own RAG engine DLL (C# or P/Invoke to native C) | Wrap in `IKnowledgeProvider` adapter — see below |
| SQL full-text search (SQL Server, PostgreSQL) | Query in `SearchAsync`, return matching chunks |
| Snowflake | Use Snowflake .NET connector in `SearchAsync` |
| Vector store (Qdrant, pgvector, Weaviate, Pinecone, Chroma) | Call the store's SDK in `SearchAsync` |

### Wrapping your own RAG DLL

If you have an existing RAG engine as a managed C# DLL (or a native C DLL accessed via P/Invoke), wrap it in a thin adapter:

```csharp
using Valaiorp.Knowledge.Contracts;

// Managed C# DLL adapter
public sealed class MyRagEngineProvider : IKnowledgeProvider
{
    public string Id => "rag-engine";
    public string Name => "RAG Engine";
    public string Description => "Custom RAG engine DLL.";
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public async Task<IReadOnlyCollection<string>> SearchAsync(
        string query, int maxResults, CancellationToken ct = default)
    {
        // Call your DLL — replace with actual types from your assembly
        var results = await MyRagEngine.SearchAsync(query, maxResults, ct);
        return results.Select(r => r.Content).ToArray();
    }
}
```

If your RAG engine is a **native C DLL**, use P/Invoke:

```csharp
using System.Runtime.InteropServices;

public sealed class NativeRagProvider : IKnowledgeProvider
{
    public string Id => "native-rag";
    public string Name => "Native RAG Engine";
    public string Description => "P/Invoke wrapper for native C RAG engine.";
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    [DllImport("rag_engine.dll", CharSet = CharSet.Utf8, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr rag_search(string query, int maxResults);

    [DllImport("rag_engine.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void rag_free(IntPtr result);

    public Task<IReadOnlyCollection<string>> SearchAsync(
        string query, int maxResults, CancellationToken ct = default)
    {
        var ptr = rag_search(query, maxResults);
        try
        {
            var json = Marshal.PtrToStringUTF8(ptr) ?? "[]";
            var results = System.Text.Json.JsonSerializer.Deserialize<string[]>(json)
                          ?? Array.Empty<string>();
            return Task.FromResult<IReadOnlyCollection<string>>(results);
        }
        finally
        {
            rag_free(ptr);
        }
    }
}
```

### Registration

```csharp
var knowledgeResolver = runtime.GetService<KnowledgeProviderResolver>();

// Register one or more providers; set your primary as default
knowledgeResolver.RegisterProvider(new MyRagEngineProvider(), setAsDefault: true);

// Optionally register a fallback
knowledgeResolver.RegisterProvider(new SqlFallbackProvider());
```

Multiple providers can be registered — the orchestrator picks the default unless a planner or step requests a specific provider by ID.

---

## Integrating with Microsoft DI (ASP.NET Core / Worker Service)

```csharp
using Valaiorp.Configuration;
using Valaiorp.Runtime;

// Program.cs
builder.Services.AddAgenticAIRuntime(new ValaiorpConfig());

public class AgentService(AgentRuntime runtime)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var result = await runtime.ExecuteAsync(new MyExecutionContext(), ct);
    }
}
```

---

## Execution Modes

| Mode | Behaviour |
|------|-----------|
| `Sequential` | Steps run one after another |
| `Parallel` | Steps run concurrently up to `MaxDegreeOfParallelism` |
| `Hybrid` | Mode set per `PlanStep` |

```csharp
config.Execution.Mode = ExecutionMode.Parallel;
runtime.SwitchMode(ExecutionMode.Parallel);
```

---

## Persistence

Valaiorp supports two persistence tiers. Choose one or combine both.

---

### Option 1 — Local JSON / JSONL files

The `ExternalExecutionLogger` (in `Valaiorp.Logging`) already writes structured execution data to disk every run. No database required.

```csharp
using Valaiorp.Tools.Enhanced.Logging;

builder.Services.AddSingleton<IExecutionLogger>(
    _ => new ExternalExecutionLogger(logDirectory: "logs"));
```

This produces per-execution files under `logs/`:

| File | Contains |
|------|----------|
| `plan_{id}.jsonl` | Plan steps, tool IDs, planner type |
| `run_{id}.jsonl` | Execution ID, overall status, duration, error |
| `step_{id}.jsonl` | Per-step name, input, output, status |

Read them back at any time using `FileReader` from the same assembly:

```csharp
using Valaiorp.Tools.Enhanced.Components;

var reader = new FileReader();

// Read a specific run log
var runJson = await reader.ReadJsonlAsync("logs/run_abc123.jsonl", ct);

// Read a plan log
var planJson = await reader.ReadJsonlAsync("logs/plan_xyz789.jsonl", ct);
```

This is enough for audit trails, replay, and human review without any external infrastructure.

---

### Option 2 — Database or Vector Store

Implement `ILongTermMemory` (from `Valaiorp.Memory`) backed by your preferred store and register it in DI:

```csharp
using Valaiorp.Memory.Contracts;
using Valaiorp.Memory.Models;

// Example: SQL Server-backed long-term memory
public sealed class SqlLongTermMemory : ILongTermMemory
{
    private readonly SqlConnection _db;
    public SqlLongTermMemory(SqlConnection db) => _db = db;

    public async Task StoreLogAsync(ExecutionLog log, CancellationToken ct = default)
    {
        // INSERT INTO execution_logs ...
    }

    public async Task<IReadOnlyList<ExecutionLog>> RetrieveLogsAsync(
        string contextId, CancellationToken ct = default)
    {
        // SELECT ... WHERE context_id = @contextId
    }

    // Implement StoreAsync, RetrieveAsync, StoreFeedbackAsync, RetrieveFeedbackAsync
}
```

```csharp
// Replace the default in-memory implementation
builder.Services.AddSingleton<ILongTermMemory, SqlLongTermMemory>();
```

The same pattern works for **Snowflake**, **PostgreSQL**, **CosmosDB**, or any vector store — one class, one registration. The rest of the framework is unaffected.

---

```csharp
var memory = runtime.GetService<MemoryManager>();

// Short-term (session-scoped, in-memory)
await memory.ShortTerm.SetAsync("current-step", "search", ct);
var step = await memory.ShortTerm.GetAsync<string>("current-step", ct);
await memory.ShortTerm.RemoveAsync("current-step", ct);

// Long-term (persistent across sessions)
await memory.LongTerm.StoreAsync(context.Id, context, ct);
await memory.LongTerm.StoreLogAsync(new ExecutionLog
{
    ContextId = context.Id,
    StepId    = "step-1",
    StepName  = "Search",
    Input     = "query",
    Output    = "results",
    Duration  = TimeSpan.FromMilliseconds(42)
}, ct);

var logs = await memory.LongTerm.RetrieveLogsAsync(context.Id, ct);
```

---

## Error Handling

```csharp
// Execution failure
var error = new ExecutionError("EXEC_001", "Step failed", innerException);

// Validation failure
var vError = new ValidationError("Input cannot be empty",
    metadata: new Dictionary<string, object> { ["field"] = "UserId" });

Console.WriteLine(error.Code);   // "EXEC_001"
Console.WriteLine(vError.Code);  // "VALIDATION_ERROR"
```

---

## Requirements

- .NET 10 runtime
- `Microsoft.Extensions.DependencyInjection` (transitive via `Valaiorp.Runtime` and `Valaiorp.Retry`)
- Windows only: `Valaiorp.BasicTools` UITools require `net10.0-windows`
- Optional: Playwright NuGet + `PLAYWRIGHT_ENABLED` define for browser automation
