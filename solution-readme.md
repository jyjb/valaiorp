# Valaiorp Execution Framework

A production-grade, modular, and extensible **Agentic AI Execution Framework** built in **.NET 10** using **Clean Architecture**, **Dependency Injection**, and **async/await** patterns. Designed for **thread-safe**, **deterministic/non-deterministic** execution of AI agents with support for tools, memory, planning, policy enforcement, observability, retry, execution logging, LLM providers, multi-agent orchestration, and human escalation.

---

## Overview

This framework enables the development of **autonomous AI agents** that can:

- Execute **structured plans** (DAG-based task graphs)
- Use **built-in and custom tools** (file, folder, API, UI automation, browser)
- Call **LLM providers** (Anthropic, OpenAI, Ollama) via a unified interface
- Coordinate **multiple agents** in orchestrator/sub-agent patterns
- Leverage **short-term and long-term memory** with conversation history
- Enforce **policies** (pre/post-execution validation and governance)
- Support **parallel, sequential, or hybrid execution**
- **Automatically retry** failed tool calls with exponential backoff and circuit breaking
- **Log every execution** (plan, steps, run) with pluggable local or file-based loggers
- Integrate **observability** (console logging, tracing, metrics)
- Handle **human escalation** (approvals, overrides, manual intervention)
- Switch between **deterministic and agentic modes**

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────────────┐
│                               Valaiorp                                    │
├─────────────┬───────────────┬──────────────┬─────────────┬───────────────┤
│    Core     │ Configuration │    Memory    │    Tools    │  BasicTools   │
├─────────────┼───────────────┼──────────────┼─────────────┼───────────────┤
│   Policy    │   Knowledge   │   Planner    │  Execution  │     Retry     │
├─────────────┼───────────────┼──────────────┼─────────────┼───────────────┤
│   Logging   │ Observability │ LlmProviders │ MultiAgent  │  Escalation   │
├─────────────┴───────────────┴──────────────┴─────────────┴───────────────┤
│                              Runtime                                      │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## Modules

| Module | Purpose | Dependencies |
|--------|---------|--------------|
| **Core** | Foundational contracts (`IExecutionContext`, `IExecutionResult`, `IAgent`, `ILlmClient`, enums, errors) | None |
| **Configuration** | Runtime settings (execution, planner, knowledge, parallelism, governance, LLM, autonomy) | Core |
| **Memory** | Short-term (session) and long-term memory, conversation history. Long-term ships with an in-memory default; replace `ILongTermMemory` for SQL, Snowflake, or vector store persistence. | Core |
| **Tools** | `ITool`, `IModule`, registries, and `ToolResolver` | Core |
| **BasicTools** | Built-in tools: file (JSON/JSONL/TXT/CSV/TSV/PSV/XML), folder, API (HTTP), Windows UIAutomation, Playwright browser | Core, Tools |
| **Knowledge** | `IKnowledgeProvider` interface and resolver — plug in your own RAG engine DLL, SQL full-text, Snowflake, or any vector store (Qdrant, pgvector, Weaviate, Pinecone) | Core |
| **Policy** | Pre/post-execution rule evaluation and governance | Core |
| **Planner** | Plan generation using Internal, Cognitive (DLL), LLM, or AutonomyAware planners | Core, Tools, Knowledge |
| **Execution** | Parallel/sequential/hybrid execution engine, workflow builder, transactions, rollback | Core, Planner, Tools, Memory, Policy, Retry |
| **Retry** | Composite retry: max-attempts + exponential backoff + circuit breaker (automatic) | Core |
| **Logging** | Structured execution logging (plan/step/run) to in-memory or JSONL files on disk. JSONL files are the lightweight persistence tier — `FileReader` reads them back for audit and replay without a database. | Core, Tools, Memory, Planner, Execution |
| **Observability** | Console logger, distributed tracing, metrics | Core |
| **LlmProviders** | `ILlmClient` implementations for Anthropic, OpenAI, and Ollama — no vendor SDK required | Core, Configuration |
| **MultiAgent** | Agent registry, multi-agent orchestration with parallel/sequential delegation and conversation memory | Core, Memory |
| **Escalation** | Human-in-the-loop: approval requests, override handling, escalation hooks | Core |
| **Runtime** | Orchestrates all modules, DI wiring, config loading | All |

---

## Getting Started

### Prerequisites

- **.NET 10 SDK**
- **C# 13**

---

### 1. Clone and build

```bash
git clone <repository-url>
cd valaiorp
dotnet build
```

---

### 2. Configure

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

---

### 3. Run an agent

```csharp
using Valaiorp.Runtime;

await using var runtime = RuntimeBuilder.BuildFromFile("appsettings.json");

var context = new MyExecutionContext(); // Implement IExecutionContext
var result = await runtime.ExecuteAsync(context);
```

---

## Retry

Every tool call is automatically wrapped in a composite retry strategy.

| Policy | Default | Behaviour |
|--------|---------|-----------|
| `MaxAttemptsRetryPolicy` | 3 attempts | Hard cap on total attempts |
| `ExponentialBackoffRetryPolicy` | 100 ms → 10 s, 5 attempts | Doubles delay on each attempt |
| `CircuitBreakerRetryPolicy` | 5 failures / 30 s reset | Stops retrying after repeated failures |

---

## Execution Logging

`AgentRuntime` writes three structured log entries per execution automatically.

| Event | Trigger | Default key |
|-------|---------|-------------|
| **Plan** | After plan creation | `execution_log_plan_{planId}` |
| **Step** | After each step | `execution_log_step_{stepId}` |
| **Run** | After executor completes | `execution_log_run_{executionId}` |

---

## Customization

### Custom Tool

```csharp
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

### Custom Planner

```csharp
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
            Steps = new[] { new PlanStep { Name = "Search", ToolId = "search" } }
        };
        return Task.FromResult(plan);
    }
}

var orchestrator = runtime.GetService<PlannerOrchestrator>();
orchestrator.RegisterPlanner(new MyPlanner(), setAsDefault: true);
```

### Custom Policy Rule

```csharp
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

### Custom Knowledge Provider (RAG)

```csharp
public sealed class MyDocs : IKnowledgeProvider
{
    public string Id => "my-docs";
    public string Name => "My Docs";
    public string Description => "Company knowledge base.";
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public async Task<IReadOnlyCollection<string>> SearchAsync(
        string query, int maxResults, CancellationToken ct = default)
    {
        // Replace with your retrieval logic (vector store, Elasticsearch, etc.)
        return new[] { "result1", "result2" };
    }
}

var resolver = runtime.GetService<KnowledgeProviderResolver>();
resolver.RegisterProvider(new MyDocs(), setAsDefault: true);
```

---

## Project Structure

```
valaiorp/
├── Core/               # Foundational contracts, enums, errors
├── Configuration/      # Runtime settings and config models
├── Memory/             # Short-term, long-term, conversation memory
├── Tools/              # ITool, IModule, registries
├── BasicTools/         # Built-in file, folder, API, UIAutomation, browser tools
├── Knowledge/          # IKnowledgeProvider — RAG integration point
├── Policy/             # Policy rules and enforcement engine
├── Planner/            # Plan generation (Internal / Cognitive / LLM / Autonomy-aware)
├── Execution/          # Execution engine, workflow builder, transactions
├── Retry/              # Retry policies and strategy
├── Logging/            # Execution logging (plan, step, run)
├── Observability/      # Console logger, tracing, metrics
├── LlmProviders/       # Anthropic, OpenAI, Ollama clients (no vendor SDK)
├── MultiAgent/         # Agent registry and multi-agent orchestration
├── Escalation/         # Approval, override, and escalation handling
└── Runtime/            # DI orchestration, RuntimeBuilder, AgentRuntime
```

---

## Key Features

| Feature | Description |
|---------|-------------|
| **Clean Architecture** | Strict separation of concerns, interface-driven design |
| **Dependency Injection** | Fully DI-compatible (Microsoft.Extensions.DependencyInjection) |
| **Async/Await** | Non-blocking, scalable execution throughout |
| **Determinism Control** | Configurable levels: `Deterministic`, `SemiDeterministic`, `NonDeterministic` |
| **Configurable Autonomy** | Scale from 0.0 (fully deterministic) to 1.0 (fully agentic) |
| **Parallel Execution** | Bounded parallelism with configurable degree |
| **Transaction Support** | Rollback on failure, commit on success |
| **Automatic Retry** | Composite retry (max-attempts + exponential backoff + circuit breaker) |
| **Execution Logging** | Plan, step, and run events logged automatically |
| **Observability** | Structured logging, distributed tracing, metrics |
| **Policy Enforcement** | Pre/post-execution validation and governance rules |
| **Multi-Planner** | Internal, Cognitive (DLL), LLM, and Autonomy-aware planners |
| **Built-in Tools** | File I/O (7 formats), folder ops, HTTP API, Windows UIAutomation, Playwright |
| **LLM Providers** | Anthropic, OpenAI, Ollama — no vendor SDK, raw HttpClient |
| **Multi-Agent** | Orchestrator/sub-agent delegation with parallel dispatch and conversation memory |
| **Human Escalation** | Approval workflows, override hooks, manual intervention interfaces |
| **Config-Driven** | All behaviour controlled via configuration |

---

## License

GPLv3. See [LICENSE](LICENSE) for details.
