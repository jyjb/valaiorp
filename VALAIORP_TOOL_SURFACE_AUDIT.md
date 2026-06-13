# VALAIORP — TOOL & MODULE SURFACE AUDIT DIRECTIVE
## For Claude Code — Read-Only Discovery Pass
## Context: Valaiorp is the RPA/Agentic framework. Its DLLs will be consumed
## by a separate project (THIRAVIYAN) alongside ARIVULAR, POROOL, and external LLMs.
## The purpose of this audit is to determine what tool and module description
## surface Valaiorp already exposes, so ARIVULAR can receive a complete runtime
## tool context at request time without a fixed registry.

---

## RULES

- Read only. No modifications, no suggestions inline with findings.
- Report what exists in source, not what should exist.
- If something is unclear or ambiguous, flag it rather than assuming.
- Focus exclusively on the tool and module description surface —
  not execution, not queue, not memory, not guardrails.
- Where source is not available and only compiled DLLs exist,
  use reflection-based inspection to surface the public API.

---

## CONTEXT — WHY THIS AUDIT EXISTS

ARIVULAR generates operational plans by reasoning over available tools.
For this to work, ARIVULAR must receive — at request time — a structured
description of every tool and module available to the current agent.

This means ARIVULAR needs to know, per tool/module:
- What it does (description)
- What inputs it accepts (names, types, whether required, what they mean)
- What outputs it produces (names, types, what they mean)
- What category it belongs to (UI automation, file, API, data, custom)
- Whether it is a tool, a module, or a module-wrapped tool

The audit must determine how much of this surface already exists in
Valaiorp's contracts and implementations, and where the gaps are.

---

## SCOPE

Audit the following across all Valaiorp assemblies:

1. `ITool` contract — what every tool must expose
2. `IModule` contract — what every module must expose
3. `ModuleTool` / `ModuleExecutor` — how modules surface as tools
4. `ToolRegistry` / `ModuleRegistry` — what metadata is accessible at runtime
5. All 14 built-in tools in `Valaiorp.BasicTools` — actual description surface
6. `ParameterDefinition` (or equivalent) — whether input/output schemas exist
7. `ToolType` enum — what categories are defined
8. Any existing serialization of tool/module metadata to JSON or structured format

---

## WHAT TO REPORT FOR EACH CONTRACT / CLASS

### For `ITool`
- Full interface definition — every property and method
- Which properties carry human-readable description
- Whether input parameter schema is part of the contract
- Whether output schema is part of the contract
- Whether there is any metadata dictionary and what it carries in practice
- Whether `ToolType` is part of the contract and what values exist

### For `IModule`
- Full interface definition
- Whether `Parameters` (or equivalent) defines a typed parameter schema
- Whether `ParameterDefinition` exists and what fields it has
  (name, type, required, description, default value, allowed values)
- Whether output schema is defined
- How a module surfaces its constituent tools

### For `ModuleTool` / `ModuleExecutor`
- How a module is wrapped as a tool for plan execution
- What description surface is carried through from the module to the tool wrapper
- Whether parameter schema is preserved in the wrapper

### For `ToolRegistry` / `ModuleRegistry`
- What can be enumerated at runtime
  (list all tools, get tool by id, get all metadata)
- Whether there is a bulk export / serialization method
- Whether registered tools carry their full description surface or only id + instance

### For each of the 14 built-in tools
Report in a table:

| Tool ID | Description field populated | Input schema defined | Output schema defined | Notes |
|---------|----------------------------|---------------------|----------------------|-------|

The 14 tools are:
- json-tool
- jsonl-tool
- jsonc-tool
- txt-tool
- csv-tool
- tsv-tool
- psv-tool
- xml-tool
- excel-tool
- word-tool
- folder-tool
- api-tool
- windows-ui-automation
- playwright-ui-automation

For each, also report:
- Whether inputs are formally declared or only documented in description string
- Whether outputs are formally declared or only discoverable by running the tool
- Whether the pipe-delimited `input` parameter (UIA and Playwright) has
  its action variants formally enumerated or only described in free text

### For `ParameterDefinition` (or equivalent)
- Full struct/class definition — every field
- Whether it carries: name, type, required flag, description, default value,
  allowed values / enum, example values
- Whether it is used consistently across built-in tools and custom tool contract

### For `ToolType` enum
- All defined values
- Whether there is a value for UI automation, browser automation, API,
  file/data, custom — or whether these are collapsed

---

## SERIALIZATION SURFACE

Report whether any of the following exist:

- A method or helper that serializes a tool or module to JSON
- A `ToolDescriptor` or `ToolManifest` type (or similar) that is a
  plain data object representing a tool without its executable
- Any existing integration with LLM function-calling schemas
  (OpenAI function schema, Anthropic tool schema, etc.)
- Whether `Valaiorp.LlmProviders` carries any tool-to-LLM-schema conversion

---

## THIRAVIYAN INTEGRATION CONTEXT

The audit findings will directly inform what needs to be built in THIRAVIYAN
to bridge Valaiorp tools into ARIVULAR's runtime tool context.

Specifically, THIRAVIYAN needs to:
1. Enumerate all registered tools and modules from Valaiorp at agent startup
2. Serialize them into a structured description ARIVULAR can consume
3. Pass that description alongside each objective to ARIVULAR
4. Receive ARIVULAR's plan referencing tool IDs
5. Execute the plan via Valaiorp's runtime
6. Feed step results back to ARIVULAR's session

Understanding what Valaiorp already exposes determines how much of
steps 1 and 2 need to be built from scratch versus assembled from
existing Valaiorp surface.

---

## OUTPUT FORMAT

```
CONTRACT SURFACE
  └─ ITool                    [FULL | PARTIAL | MINIMAL]
  └─ IModule                  [FULL | PARTIAL | MINIMAL]
  └─ ModuleTool/Executor      [FULL | PARTIAL | MINIMAL]
  └─ ToolRegistry             [FULL | PARTIAL | MINIMAL]
  └─ ModuleRegistry           [FULL | PARTIAL | MINIMAL]
  └─ ParameterDefinition      [EXISTS | PARTIAL | ABSENT]

BUILT-IN TOOL SURFACE TABLE
  (table as specified above — one row per tool)

SERIALIZATION SURFACE
  └─ JSON serialization method  [EXISTS | ABSENT]
  └─ Descriptor/Manifest type   [EXISTS | ABSENT]
  └─ LLM schema conversion      [EXISTS | ABSENT]

GAPS
  A numbered list of everything missing that THIRAVIYAN will need to build
  or extend to give ARIVULAR a complete runtime tool context.
  Each gap: what is missing, where it is missing, what impact it has.

THIRAVIYAN BUILD REQUIREMENTS
  Based on the gaps, a precise list of what THIRAVIYAN needs:
  - What it can assemble from existing Valaiorp surface as-is
  - What needs a thin extension to Valaiorp contracts
  - What must be built entirely in THIRAVIYAN

VERDICT
  [SURFACE SUFFICIENT — THIRAVIYAN can assemble tool context from existing Valaiorp |
   SURFACE PARTIAL — minor extensions needed to Valaiorp contracts |
   SURFACE INSUFFICIENT — significant work needed before ARIVULAR integration is possible]
```

---

## CLASSIFICATION OF GAPS

| Class | Definition |
|-------|-----------|
| BLOCKING | ARIVULAR cannot reason about this tool/module without this information |
| HIGH | ARIVULAR will produce lower quality plans without this information |
| MEDIUM | Nice to have for plan quality; workaround exists |
| LOW | Cosmetic or observability gap only |
