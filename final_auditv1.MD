# VALAIORP — TOOL & MODULE SURFACE AUDIT
**Date:** 2026-05-25  
**Scope:** Read-only discovery pass across all Valaiorp assemblies  
**Purpose:** Determine what tool and module description surface Valaiorp already exposes so THIRAVIYAN can assemble a complete runtime tool context for ARIVULAR.

---

## CONTRACT SURFACE

```
CONTRACT SURFACE
  └─ ITool                    [PARTIAL]
  └─ IModule                  [PARTIAL]
  └─ ModuleTool/Executor      [PARTIAL]
  └─ ToolRegistry             [PARTIAL]
  └─ ModuleRegistry           [MINIMAL]
  └─ ParameterDefinition      [PARTIAL]
```

---

## CONTRACT DETAIL

### ITool — `Tools/Contracts/ITool.cs`

```csharp
public interface ITool
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    ToolType Type { get; }
    IReadOnlyDictionary<string, object> Metadata { get; }
    Task<ToolResult> ExecuteAsync(IExecutionContext context, IReadOnlyDictionary<string, object> parameters, CancellationToken ct = default);
}
```

| Aspect | Status | Notes |
|--------|--------|-------|
| Human-readable description | `Description` (string) — populated on all 14 built-ins | Free text only; no machine-readable schema |
| Input parameter schema | **ABSENT** — no `Parameters` or `InputSchema` property | All 14 tools parse `IReadOnlyDictionary<string,object>` directly |
| Output schema | **ABSENT** — no output type or schema property | Output shape discoverable only by execution |
| Metadata dictionary | Present — `IReadOnlyDictionary<string,object>` | Used inconsistently; carries `SupportedExtensions`, `SupportedActions`, or `Platform` per tool; no enforced contract for contents |
| ToolType | Present — `ToolType Type` | See ToolType section below |

**Verdict:** PARTIAL. Human-readable description surface exists. No machine-readable input or output schema.

---

### IModule — `Tools/Contracts/IModule.cs`

```csharp
public interface IModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyDictionary<string, ParameterDefinition> Parameters { get; }
    IReadOnlyCollection<ITool> Tools { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task<ModuleResult> ExecuteAsync(IExecutionContext context, IReadOnlyDictionary<string, object> parameters, CancellationToken ct = default);
}
```

| Aspect | Status | Notes |
|--------|--------|-------|
| Human-readable description | `Description` (string) | |
| Input parameter schema | `Parameters` — typed `IReadOnlyDictionary<string, ParameterDefinition>` | **Better than ITool** — formal schema exists |
| Output schema | **ABSENT** | No return type schema; `ModuleResult.Results` is `object?` |
| Constituent tool enumeration | `Tools` — `IReadOnlyCollection<ITool>` | Can walk module's tool list; each tool itself has no schema |

**Verdict:** PARTIAL. Has typed input parameter schema via `ParameterDefinition`. No output schema. `ParameterDefinition` is incomplete (see below).

---

### ModuleTool — `Modules/ModuleTool.cs`

Adapts `IModule` to `ITool` for the execution pipeline.

| Aspect | Status |
|--------|--------|
| `Id`, `Name`, `Description` | Forwarded directly from the wrapped `IModule` |
| `Type` | Fixed to `ToolType.Module` |
| `Metadata` | `module.Parameters.ToDictionary(p => p.Key, p => (object)p.Value)` |
| Parameter schema preservation | **DEGRADED** — `ParameterDefinition` objects are stored as `object` in the Metadata dictionary; type is erased for callers who only hold `ITool` |

`ModuleTool.Metadata["someParam"]` is a `ParameterDefinition` instance but declared as `object`. A consumer holding `ITool` cannot access parameter schema fields without a cast and reflection check.

---

### ModuleExecutor — `Modules/ModuleExecutor.cs`

```csharp
public Task<ModuleResult> ExecuteAsync(string moduleId, IExecutionContext context, IReadOnlyDictionary<string, object> parameters, CancellationToken ct)
public IReadOnlyDictionary<string, IModule> GetAll()
```

`GetAll()` returns all registered modules. No schema export method. Execution-only surface beyond enumeration.

---

### ToolRegistry — `Tools/Registries/ToolRegistry.cs`

```csharp
public IReadOnlyDictionary<string, ITool> Tools { get; }
public void Register(ITool tool)
public bool TryGetTool(string toolId, out ITool? tool)
public void Unregister(string toolId)
public void Clear()
public string FormatForSystemPrompt()
```

`FormatForSystemPrompt()` output format per tool:
```
- {tool.Id}: {tool.Description}
```

| Aspect | Status | Notes |
|--------|--------|-------|
| Enumerate all tools | Yes — `Tools` property | Returns `ITool` instances |
| Get tool by ID | Yes — `TryGetTool` | |
| Bulk text export | Yes — `FormatForSystemPrompt()` | Plain text only; no structure |
| Bulk JSON export | **ABSENT** | No structured serialization |
| Full metadata in registry | Full `ITool` instances available | Callers can read all fields, but no schema beyond Description |
| Descriptor/manifest type | **ABSENT** | |

---

### ModuleRegistry — `Tools/Registries/ModuleRegistry.cs`

```csharp
public IReadOnlyDictionary<string, IModule> Modules { get; }
public void Register(IModule module)
public bool TryGetModule(string moduleId, out IModule? module)
public void Unregister(string moduleId)
public void Clear()
```

No `FormatForSystemPrompt`. No export method. Enumeration only.

---

### ParameterDefinition — `Core/Contracts/ParameterDefinition.cs`

```csharp
public sealed class ParameterDefinition
{
    public required string Name        { get; init; }
    public required string Description { get; init; }
    public required string Type        { get; init; }   // free-form string, not an enum
    public bool Required               { get; init; } = true;
    public object? DefaultValue        { get; init; }
}
```

| Field | Present | Notes |
|-------|---------|-------|
| Name | Yes | |
| Description | Yes | |
| Type | Yes | Free-form string (e.g. `"string"`, `"int"`) — no enforced enum |
| Required | Yes | |
| DefaultValue | Yes | |
| AllowedValues / enum constraints | **ABSENT** | |
| ExampleValues | **ABSENT** | |
| MinValue / MaxValue / pattern | **ABSENT** | |

**Used by:** `IModule.Parameters` only. No built-in `ITool` implementation exposes `ParameterDefinition`. The 14 built-in tools do not use this class.

---

### ToolType Enum — `Core/Enums/ToolType.cs`

```csharp
public enum ToolType { None, Native, External, Custom, Module }
```

| Category | Coverage | Notes |
|----------|----------|-------|
| UI automation (desktop) | `Native` | Same value as file and folder tools — no UI-specific type |
| Browser automation (Playwright) | `External` | Same value as API calls |
| API / HTTP | `External` | |
| File / data | `Native` | |
| Custom (user-defined) | `Custom` | |
| Module (wrapped module) | `Module` | |
| Browser-specific | **ABSENT** | No `Browser`, `UIDesktop`, `UIBrowser` value |

---

## BUILT-IN TOOL SURFACE TABLE

| Tool ID | Description field populated | Input schema defined | Output schema defined | Notes |
|---------|----------------------------|---------------------|-----------------------|-------|
| json-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions |
| jsonl-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions |
| jsonc-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions; strips `//` and `/* */` comments |
| txt-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions; supports append operation |
| csv-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions; auto-converts JSON array on write |
| tsv-tool | Yes — inline text | No — raw dict only | No | Tab delimiter |
| psv-tool | Yes — inline text | No — raw dict only | No | Pipe delimiter |
| xml-tool | Yes — inline text | No — raw dict only | No | DTD processing prohibited (security) |
| excel-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions; 5 operations; no formal per-op param set |
| word-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedExtensions; 5 operations |
| folder-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedActions (FolderAction enum names as string[]) |
| api-tool | Yes — inline text | No — raw dict only | No | Metadata: SupportedMethods (ApiAction enum names); SSRF protection built-in |
| windows-ui-automation | Yes — free text with pipe format | No — raw dict only | No | Metadata: Platform, SupportedActions; 14 actions; #if WINDOWS |
| playwright-ui-automation | Yes — **markdown table embedded in Description string** | No — raw dict only | No | Metadata: Platform, SupportedActions; 14 actions + EvaluateJs; #if PLAYWRIGHT_ENABLED |

### Formal vs informal declarations per tool type

**File tools (json, jsonl, jsonc, txt, csv, tsv, psv, xml, excel, word):**
- `operation` / `filePath` / `content` are named in `Description` string only — not formally declared
- No `ParameterDefinition` exists on any file tool
- Outputs (`Content`, `Sheets`, cell values) are discovered only by running the tool

**folder-tool:**
- `action` values are in `Metadata["SupportedActions"]` as `string[]` (from FolderAction enum) — partial enumeration
- Other parameters (`path`, `destPath`, `recursive`, `pattern`) are description-only

**api-tool:**
- `method` values are in `Metadata["SupportedMethods"]` as `string[]` (from ApiAction enum) — partial enumeration
- `url`, `body`, `headers` are description-only

**windows-ui-automation:**
- Action variants are enumerated in `Metadata["SupportedActions"]` as `string[]` (from `UIAutomationAction` enum)
- The pipe-delimited input format and per-action parameter shapes are described in the `Description` free-text string only — **not formally enumerated in any schema**
- Tool also accepts flat `action` / `element` / `url` / `value` parameters as an alternative to the pipe-delimited string — this dual-input mode is not documented in the `Description`

**playwright-ui-automation:**
- Action variants are enumerated in `Metadata["SupportedActions"]` as `string[]` (from `BrowserAction` enum)
- The per-action parameter shapes are described in a **markdown table embedded inside the `Description` raw string literal** — richer than windows-ui-automation but still free text, not machine-readable
- EvaluateJs action is unique to Playwright; absent from windows-ui-automation

---

## SERIALIZATION SURFACE

```
SERIALIZATION SURFACE
  └─ JSON serialization method        [ABSENT]
  └─ Descriptor/Manifest type         [ABSENT]
  └─ LLM schema conversion (outbound) [ABSENT]
  └─ LLM tool-call parsing (inbound)  [EXISTS — GenericLlmClient]
```

### Detail

**`ToolRegistry.FormatForSystemPrompt()`** — exists, but produces plain text `"- id: description"` lines. Not JSON. Not parseable by ARIVULAR as structured tool definitions.

**ToolDescriptor / ToolManifest** — no such type exists anywhere in the codebase. No serializable plain-data object that represents a tool's callable surface detached from its executable instance.

**Outbound LLM schema conversion** — `GenericLlmClient.BuildRequestBody()` constructs LLM request messages but does **not** include a `tools` / `functions` field. There is no method in the entire codebase that converts an `ITool` or `IModule` to an Anthropic `tool_use` schema or an OpenAI `function` schema.

**Inbound LLM tool-call parsing** — `GenericLlmClient.ParseToolCalls()` **does exist** and handles both Anthropic (`stop_reason="tool_use"`, content[].type="tool_use") and OpenAI (`finish_reason="tool_calls"`, choices[0].message.tool_calls[]) response shapes. Parsed calls return `ToolCall { CallId, ToolName, Inputs }`.

**Key asymmetry:** Valaiorp can receive tool calls from LLMs but has no code path to advertise tools to LLMs. The inbound parser is complete; the outbound schema generator does not exist.

---

## GAPS

### 1. [BLOCKING] ITool has no formal input parameter schema

`ITool` exposes only a `Description` string for parameter information. There is no `Parameters` property, no `InputSchema`, and no `IReadOnlyDictionary<string, ParameterDefinition>` on the tool contract. ARIVULAR cannot determine — without parsing free text — what parameters a tool accepts, their types, which are required, or what values are valid.

**Location:** `Tools/Contracts/ITool.cs`  
**Impact:** ARIVULAR cannot generate correct tool invocations. Any plan ARIVULAR produces will rely on the LLM parsing Description strings rather than on a structured schema, producing lower reliability and no ability to validate plans before execution.

---

### 2. [BLOCKING] No ToolDescriptor or ToolManifest type

No serializable plain-data object exists that represents a tool's callable surface without holding the executable instance. THIRAVIYAN cannot serialize and transmit tool definitions to ARIVULAR without building this from scratch.

**Location:** Absent across all assemblies  
**Impact:** THIRAVIYAN must build a descriptor type (or use a third-party schema format like JSON Schema) to bridge Valaiorp tools into ARIVULAR's tool context.

---

### 3. [BLOCKING] No outbound LLM schema conversion

`GenericLlmClient` can parse incoming LLM tool calls (Anthropic + OpenAI formats). It cannot generate the `tools` / `functions` block in the LLM request that tells the LLM what tools are available. No code path converts `ITool` or `IModule` to Anthropic `tool_use` schema or OpenAI function schema.

**Location:** `LlmProviders/Clients/GenericLlmClient.cs` — `BuildRequestBody()` has no tools parameter  
**Impact:** ARIVULAR cannot receive a structured tool manifest from THIRAVIYAN unless THIRAVIYAN builds the schema conversion itself. Until this exists, tools can only be communicated via Description strings injected into the system prompt — which is imprecise and wastes context.

---

### 4. [BLOCKING] UI tool action variants not surfaced in structured schema

`UIAutomationAction` (14 values) and `BrowserAction` (14 values) are C# enums in source but their per-action parameter shapes are only described in the `Description` string. `Metadata["SupportedActions"]` carries the action names as `string[]`, but there is no per-action parameter definition (e.g., what params Navigate takes vs. SetText vs. Screenshot). ARIVULAR cannot reason about which action to use without parsing Description free text.

**Location:** `BasicTools/UITools/WindowsUIAutomationTool.cs`, `BasicTools/BrowserTools/PlaywrightUIAutomationTool.cs`  
**Impact:** ARIVULAR's plans for UI automation will be unreliable without structured per-action schemas.

---

### 5. [HIGH] ToolRegistry.FormatForSystemPrompt() is text-only

The only bulk export method produces `"- id: description"` lines. THIRAVIYAN needs a structured (JSON/object) enumerator that returns id, name, description, type, and parameter schema for each tool in a format ARIVULAR can consume programmatically.

**Location:** `Tools/Registries/ToolRegistry.cs`  
**Impact:** THIRAVIYAN cannot call a single method to get a structured tool manifest. It must iterate `ToolRegistry.Tools`, read each property, and build the manifest itself.

---

### 6. [HIGH] ModuleRegistry has no export or description method

`ModuleRegistry` exposes only `.Modules` (raw dictionary) and CRUD. There is no `FormatForSystemPrompt()`, no JSON export, no descriptor method. Module descriptions are not accessible in bulk.

**Location:** `Tools/Registries/ModuleRegistry.cs`  
**Impact:** THIRAVIYAN must iterate `.Modules.Values` manually to assemble module descriptions for ARIVULAR.

---

### 7. [HIGH] ParameterDefinition missing AllowedValues and ExampleValues

Even where `ParameterDefinition` is used (`IModule.Parameters`), ARIVULAR cannot learn what values are valid for a given parameter. There is no `AllowedValues: string[]`, no `EnumValues`, no `ExampleValue`. This prevents ARIVULAR from reasoning about, for example, which operations an `excel-tool`-backed module supports.

**Location:** `Core/Contracts/ParameterDefinition.cs`  
**Impact:** Lower plan quality for modules. ARIVULAR must guess or default for parameters with enumerated valid values.

---

### 8. [HIGH] ITool has no output schema

`ITool.ExecuteAsync` returns `ToolResult` where `Results` is `object?`. No tool declares the structure of its output. ARIVULAR cannot reason about what data will be available after a tool step executes without either running the tool or reading implementation source code.

**Location:** `Tools/Contracts/ITool.cs`, `Core/Contracts/ToolResult.cs`  
**Impact:** ARIVULAR cannot build plans that chain tool outputs to subsequent tool inputs without guessing output shape. This reduces plan reliability, especially for data-pipeline plans.

---

### 9. [MEDIUM] ToolType enum lacks categorical granularity

`ToolType.Native` covers file tools, folder tools, and Windows UI automation. `ToolType.External` covers both HTTP API calls and Playwright browser automation. ARIVULAR cannot filter available tools by capability class (e.g. "all browser tools", "all file tools") using `ToolType` alone.

**Location:** `Core/Enums/ToolType.cs`  
**Impact:** THIRAVIYAN must implement its own category mapping if ARIVULAR needs to filter by tool capability. Workaround: use `Metadata["Platform"]` or `Metadata["SupportedExtensions"]` but these are inconsistent across tools.

---

### 10. [MEDIUM] ModuleTool.Metadata stores ParameterDefinition as object

When `IModule` is wrapped as `ITool` via `ModuleTool`, its `Parameters` (typed `ParameterDefinition` objects) are stored in `Metadata` as `object`. A consumer holding only `ITool` cannot access parameter schema fields without casting each Metadata value and checking its runtime type.

**Location:** `Modules/ModuleTool.cs`  
**Impact:** THIRAVIYAN code consuming `ITool` references to modules cannot access parameter schema without down-casting. This is a usability gap, not a blocking one, since `IModule` is accessible directly via `ModuleRegistry`.

---

### 11. [LOW] SupportedActions in Metadata is not linked to per-action parameter shapes

`folder-tool`, `api-tool`, `windows-ui-automation`, and `playwright-ui-automation` all put action/method name arrays in `Metadata["SupportedActions"]` or `Metadata["SupportedMethods"]`. However, this only lists the action names. There is no corresponding data structure mapping each action to its parameters.

**Location:** `BasicTools/FolderTools/FolderTool.cs`, `BasicTools/ApiTools/ApiTool.cs`, `BasicTools/UITools/WindowsUIAutomationTool.cs`, `BasicTools/BrowserTools/PlaywrightUIAutomationTool.cs`  
**Impact:** Partially useful (action names are known) but incomplete without per-action schemas.

---

## THIRAVIYAN BUILD REQUIREMENTS

### What can be assembled from existing Valaiorp surface as-is

- **Tool enumeration:** `ToolRegistry.Tools` and `ModuleRegistry.Modules` — all registered tools and modules are accessible
- **Tool identity:** `Id`, `Name`, `Description`, `Type` — all populated on every built-in tool
- **Module parameter schemas:** `IModule.Parameters` gives typed `ParameterDefinition` for modules (Name, Description, Type, Required, DefaultValue)
- **Module constituent tools:** `IModule.Tools` gives the list of tools a module contains
- **Action name lists:** `Metadata["SupportedActions"]` / `Metadata["SupportedMethods"]` on ui and action-based tools give string arrays of action names
- **Inbound tool-call parsing:** `GenericLlmClient` already parses Anthropic and OpenAI tool call responses — THIRAVIYAN can reuse this

### What needs a thin extension to Valaiorp contracts

| Extension | Where | What to add |
|-----------|-------|-------------|
| `ITool` input schema | `Tools/Contracts/ITool.cs` | Add `IReadOnlyDictionary<string, ParameterDefinition>? Parameters` (nullable — keeps backward compat for tools that don't define one) |
| `ParameterDefinition` constraints | `Core/Contracts/ParameterDefinition.cs` | Add `IReadOnlyList<object>? AllowedValues` and `object? ExampleValue` fields |
| `ToolRegistry` structured export | `Tools/Registries/ToolRegistry.cs` | Add `IReadOnlyCollection<ToolDescriptor> GetDescriptors()` returning plain data objects |
| `ModuleRegistry` export | `Tools/Registries/ModuleRegistry.cs` | Add `FormatForSystemPrompt()` or equivalent structured export |
| `ToolType` granularity | `Core/Enums/ToolType.cs` | Add `Browser`, `UIDesktop`, `UIBrowser` (or rely on Metadata for now — see below) |

### What must be built entirely in THIRAVIYAN

| Component | Description |
|-----------|-------------|
| `ToolDescriptor` / `ToolManifest` type | Plain data object: `Id`, `Name`, `Description`, `Type`, `Parameters` (list of param defs), `OutputShape` (optional). Serializable to JSON / Anthropic schema / OpenAI schema. |
| Outbound LLM schema converter | Method to convert `ToolDescriptor` → Anthropic `tool_use` input schema or OpenAI function schema. THIRAVIYAN injects this into the `GenericLlmClient` request (or passes it to ARIVULAR pre-formatted). |
| Tool context assembler | Startup component that: (1) calls `ToolRegistry.Tools` + `ModuleRegistry.Modules`, (2) builds `ToolDescriptor` for each, (3) serializes to the format ARIVULAR expects. |
| Per-action schema definitions for UI tools | Since `UIAutomationAction` and `BrowserAction` per-action parameter shapes are not in Valaiorp source, THIRAVIYAN must define these manually or via config. Each action (Navigate, SetText, Screenshot, etc.) needs its own parameter list. |
| Plan-to-execution bridge | Receives ARIVULAR's plan (tool IDs + parameters), calls `ToolResolver.ExecuteToolAsync` per step, feeds results back to ARIVULAR. (`ToolResolver` exists; the bridge wrapping it does not.) |

---

## VERDICT

**SURFACE PARTIAL — moderate extensions needed to Valaiorp contracts before ARIVULAR integration is viable**

The description surface foundation is solid: all 14 built-in tools have populated `Description` strings, `IModule` has a typed `ParameterDefinition` parameter schema, and the registry/enumeration system is thread-safe and complete. `GenericLlmClient` already handles inbound tool-call parsing for both Anthropic and OpenAI.

However, 4 BLOCKING gaps mean THIRAVIYAN cannot assemble a complete machine-readable tool context for ARIVULAR from the existing surface alone:
1. `ITool` has no formal input parameter schema
2. No `ToolDescriptor`/`ToolManifest` type exists
3. No outbound LLM schema conversion
4. UI tool per-action parameter shapes are free-text only

The required extensions to Valaiorp are additive (no breaking changes needed) and moderate in scope. The majority of THIRAVIYAN's bridge work — tool context assembly, schema conversion, and plan execution — must be built in THIRAVIYAN itself rather than delegated to Valaiorp.

---

## APPENDIX — CLASSIFICATION REFERENCE

| Class | Definition |
|-------|-----------|
| BLOCKING | ARIVULAR cannot reason about this tool/module without this information |
| HIGH | ARIVULAR will produce lower quality plans without this information |
| MEDIUM | Nice to have for plan quality; workaround exists |
| LOW | Cosmetic or observability gap only |
