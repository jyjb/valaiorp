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
using Valaiorp.Configuration.Config;
using Valaiorp.Core.Enums;

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
├─────────────┼───────────────┼──────────────┼──────────────┼───────────────┤
│   Policy    │  Guardrails   │                                               │
├─────────────┴───────────────┴──────────────────────────────────────────────┤
│                               Runtime                                      │
│     AgentRuntime · BotWorker · InMemoryWorkQueue · JsonlWorkQueue          │
│     SqlWorkQueue (abstract, plug in any ADO.NET provider)                  │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Getting Started

### Install

```
dotnet add package Valaiorp
```

### IRPA — Pure Automation (no AI)

```csharp
var config = new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile();

await using var runtime = RuntimeBuilder.Build(config);
runtime.GetService<PlannerOrchestrator>()
       .RegisterPlanner(new MySapWorkflowPlanner(), setAsDefault: true);

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

## Multi-Bot / Multi-Machine Deployment

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
                          │  InMemory (dev)  │
                          │  JSONL  (local)  │
                          │  SQL    (prod)   │
                          └──────────────────┘
```

All bots compete for items from the same queue. `SqlWorkQueue` uses `SELECT FOR UPDATE SKIP LOCKED` — no two bots ever claim the same transaction.

```csharp
await using var bot = RuntimeBuilder.BuildBot(
    queueId:        "sap-invoices",
    botId:          "sap-bot",
    config:         new ValaiorpConfig { WorkflowType = WorkflowType.Irpa }.ApplyProfile(),
    queue:          new MyQueue(),
    maxConcurrency: 8,
    maxAttempts:    3,
    configureModules: m => m.Register(new SapLoginModule()));

bot.GetService<PlannerOrchestrator>().RegisterPlanner(new SapInvoicePlanner(), setAsDefault: true);

await bot.StartAsync();
```

---

## Key Features

| Feature | Description |
|---------|-------------|
| **4 Workflow Types** | IRPA → AI Workflow → AI Agent → Agentic — one framework, one config switch |
| **3 AI Participation Modes** | Observe Only · Observe & Suggest · Observe & React |
| **WorkflowProfile presets** | `ApplyProfile()` sets all planner and autonomy flags from two enums |
| **Multi-Bot / Multi-Machine** | BotWorker + shared IWorkQueue — N bots, N machines, zero coordination code |
| **3 Queue Backends** | InMemory (dev) · JSONL (local) · SQL (production, any ADO.NET provider) |
| **Atomic Item Claim** | `SELECT FOR UPDATE SKIP LOCKED` — no duplicate processing across bots |
| **2-Layer Retry** | Tool-level (exponential backoff + circuit breaker) + Queue-level (nack/dead-letter) |
| **Dead-Letter** | Failed items parked with reason, exception type, and full detail |
| **4 Planner Types** | `Deliberative` (code) · `Manual` (JSON) · `LlmBased` · `AutonomyAware` |
| **Variable Binding** | `${StepName.Results.Field}` resolved between plan steps at runtime |
| **10 Built-in File Formats** | JSON · JSONL · JSONC · TXT · CSV · TSV · PSV · XML · XLSX · DOCX — no extra NuGet packages |
| **Built-in Tools** | HTTP API · Folder ops · Windows UIAutomation (Win32/WPF/WinForms) · Playwright browser automation (opt-in) |
| **Modules** | Reusable multi-step tool sequences — visible to the LLM for planning |
| **Policy Enforcement** | Pre/post-execution governance rules |
| **6 Built-in Guardrails** | PII redaction · prompt injection · banned keywords · tool scope · data classification |
| **Human Escalation** | Approval workflows · override hooks · manual intervention |
| **Multi-Agent** | Orchestrator/sub-agent delegation with parallel dispatch |
| **File-backed Memory** | Short-term, long-term, and conversation memory backed by JSONL files by default — swap for Redis/SQL |
| **SQL Logging** | `AddSqlPersistence()` layers SQL execution logging alongside the mandatory local JSONL log |
| **LLM Providers** | 7 built-in profiles: Anthropic · OpenAI · Ollama · Gemini · Mistral · Cohere · NVIDIA — single `GenericLlmClient`, no vendor SDK |
| **Config File** | `ValaiorpConfig` can be loaded from `valaiorp.json` via `RuntimeBuilder.BuildFromFile()` |
| **Clean Architecture** | Interface-driven, strictly layered, fully DI-compatible |

---

## Project Structure

```
Valaiorp/
├── Core/           # IWorkItem, IWorkQueue, IBotContext, ITool, IModule, IExecutionContext, enums
├── Configuration/  # WorkflowType, AiParticipation, ValaiorpConfig, WorkflowProfile, LlmConfig
├── Memory/         # Short-term, long-term, conversation memory
├── Tools/          # ToolRegistry, ModuleRegistry, ToolResolver, ToolParameters helpers
├── BasicTools/     # 10 built-in file/folder/API/UIAutomation tools
├── Modules/        # BaseModule, ModuleTool, ModuleExecutor
├── Knowledge/      # IKnowledgeProvider — RAG integration
├── Policy/         # PolicyRule, IPolicyEngine
├── Guardrails/     # IGuardrail, IGuardrailPipeline, 6 built-in guardrails
├── Planner/        # Planners, PlannerOrchestrator, plan.schema.json
├── Execution/      # ParallelExecutor, variable binding, transactions
├── Retry/          # MaxAttempts, ExponentialBackoff, CircuitBreaker policies
├── Logging/        # Plan/step/run logging
├── Observability/  # Console logger, tracing, metrics
├── LlmProviders/   # GenericLlmClient + 7 built-in JSON profiles (Anthropic, OpenAI, Ollama, Gemini, Mistral, Cohere, NVIDIA)
├── MultiAgent/     # IAgent, IAgentRegistry, MultiAgentOrchestrator
├── Escalation/     # IApprovalProvider, IEscalationService
└── Runtime/        # AgentRuntime, BotWorker, queue backends, RuntimeBuilder
```

---

## Requirements

- .NET 10 runtime
- `Microsoft.Extensions.DependencyInjection` (transitive via `Valaiorp.Runtime`)
- Windows only: UIAutomation tools (`windows-ui-automation`) require `net10.0-windows` TFM
- Optional: `Microsoft.Playwright` NuGet + `PLAYWRIGHT_ENABLED` compile constant for Playwright browser automation
- Optional: ADO.NET provider NuGet (e.g. `Microsoft.Data.SqlClient`, `Npgsql`, `Microsoft.Data.Sqlite`) for `SqlWorkQueue` / `AddSqlPersistence`

---

## License

Apache 2.0 — see [LICENSE](https://github.com/valaiorp/valaiorp/blob/main/LICENSE) for details.
