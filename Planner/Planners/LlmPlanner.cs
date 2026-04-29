namespace Valaiorp.Planner.Planners
{
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Observability.Contracts;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Registries;

    /// <summary>
    /// LLM-driven planner. Injects tool descriptions + RAG/memory context into the prompt,
    /// calls ILlmClient, and parses the JSON plan response.
    /// Subclass and override BuildSystemPrompt / ParsePlan to customise behaviour.
    /// </summary>
    public class LlmPlanner : IPlanner
    {
        private readonly ILlmClient _llmClient;
        private readonly ToolRegistry _toolRegistry;
        private readonly ModuleRegistry _moduleRegistry;
        private readonly ILogger _logger;

        public LlmPlanner(
            ILlmClient llmClient,
            ToolRegistry toolRegistry,
            ModuleRegistry moduleRegistry,
            ILogger? logger = null)
        {
            _llmClient = llmClient;
            _toolRegistry = toolRegistry;
            _moduleRegistry = moduleRegistry;
            _logger = logger ?? NullLogger.Instance;
            Id = nameof(LlmPlanner);
        }

        public string Id { get; protected set; }
        public PlannerType Type { get; protected set; } = PlannerType.Hierarchical;
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.NonDeterministic;

        public virtual async Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            var promptContext = BuildPrompt(context);
            var response = await _llmClient.CompleteAsync(promptContext, ct).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                await _logger.LogAsync(
                    LogLevel.Error,
                    $"[LlmPlanner] LLM call failed for context '{context.Id}': {response.Error}",
                    correlationId: context.Id,
                    ct: ct).ConfigureAwait(false);

                throw new InvalidOperationException(
                    $"LLM planning failed for context '{context.Id}': {response.Error}");
            }

            var plan = ParsePlan(context, response.Content);
            plan.PlanningTokens = TokenUsage.From(response);
            return plan;
        }

        // ── Prompt construction ──────────────────────────────────────────────────

        protected virtual PromptContext BuildPrompt(IExecutionContext context)
        {
            var existing = context.Prompt;
            return new PromptContext
            {
                SystemPrompt = BuildSystemPrompt(context),
                UserPrompt = existing?.UserPrompt ?? string.Empty,
                RagContext = existing?.RagContext ?? [],
                MemoryContext = existing?.MemoryContext ?? [],
                ConversationHistory = existing?.ConversationHistory ?? [],
                Variables = existing?.Variables ?? new Dictionary<string, string>()
            };
        }

        protected virtual string BuildSystemPrompt(IExecutionContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a planning agent. Analyse the user goal and produce a structured execution plan.");
            sb.AppendLine("Return ONLY a JSON object — no markdown fences, no explanations — in this exact schema:");
            sb.AppendLine("""
{
  "steps": [
    {
      "name": "<short step name>",
      "description": "<what this step achieves>",
      "toolId": "<tool id or null>",
      "agentId": "<agent id for delegation or null>",
      "inputs": { "<key>": "<value>" },
      "mode": "Sequential",
      "priority": 1,
      "isHighRisk": false,
      "condition": "<optional — omit when not needed>",
      "isLoopStart": false,
      "isLoopEnd": false,
      "loopCondition": "<optional — omit when not needed>",
      "loopStartId": "<name of the isLoopStart step — required only when isLoopEnd is true>"
    }
  ]
}
""");
            sb.AppendLine("Rules:");
            sb.AppendLine("- Use only toolId values from the Available Tools list below.");
            sb.AppendLine("- Set mode to \"Parallel\" only for steps that are fully independent.");
            sb.AppendLine("- Lower priority numbers execute first (1 = highest).");
            sb.AppendLine("- Set agentId when delegating a sub-task to another agent.");
            sb.AppendLine("- Set isHighRisk to true for steps that write data, call external APIs, send messages, or delete anything.");
            sb.AppendLine("- To pass output from a previous step into an input, use the syntax: ${StepName.Results.FieldName}");
            sb.AppendLine("  Example: if a step named \"Fetch Data\" returns a body, reference it as ${Fetch Data.Results.Body}");
            sb.AppendLine("  Always include all required inputs for each tool based on its description.");
            sb.AppendLine();
            sb.AppendLine("Conditions (skip a step when a runtime value is not what you expect):");
            sb.AppendLine("- Set condition on any step. The step is skipped when the expression evaluates false.");
            sb.AppendLine("- Syntax: WorkflowState['key'] == value  |  != value  |  > N  |  < N  |  CONTAINS substring");
            sb.AppendLine("- WorkflowState is populated automatically from tool outputs of completed steps (keys = tool output field names).");
            sb.AppendLine("- Example: \"WorkflowState['status'] == success\"  skips the step if the previous tool did not set status=success.");
            sb.AppendLine();
            sb.AppendLine("Loops (repeat a block of steps a fixed number of times or until a state condition changes):");
            sb.AppendLine("- Mark the first step of the repeating block with  isLoopStart: true");
            sb.AppendLine("- Mark the last  step of the repeating block with  isLoopEnd: true  +  loopCondition  +  loopStartId");
            sb.AppendLine("- loopCondition syntax: \"iteration < N\"  |  \"iteration <= N\"  |  WorkflowState expression");
            sb.AppendLine("  'iteration' is a built-in counter starting at 1, incremented after each pass.");
            sb.AppendLine("- loopStartId must equal the name of the matching isLoopStart step (case-insensitive).");
            sb.AppendLine("- All steps between isLoopStart and isLoopEnd are re-executed each iteration.");
            sb.AppendLine("- Example: to read and process 5 files, make the read step isLoopStart:true, the process step isLoopEnd:true,");
            sb.AppendLine("  loopCondition:\"iteration < 5\", loopStartId:\"<name of read step>\".");

            var tools = _toolRegistry.Tools.Values.ToList();
            var modulesTools = _moduleRegistry.Modules.Values
                .SelectMany(m => m.Tools.Select(t => (module: m, tool: t)))
                .ToList();

            if (tools.Count > 0 || modulesTools.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Available Tools:");
                foreach (var t in tools)
                    sb.AppendLine($"  - {t.Id}: {t.Description}");
                foreach (var (m, t) in modulesTools)
                    sb.AppendLine($"  - {t.Id} (module: {m.Id}): {t.Description}");
            }

            if (context.Prompt is { } p && !string.IsNullOrWhiteSpace(p.SystemPrompt))
            {
                sb.AppendLine();
                sb.AppendLine("Additional Instructions:");
                sb.AppendLine(p.SystemPrompt);
            }

            if (context.Prompt?.RagContext is { Count: > 0 } rag)
            {
                sb.AppendLine();
                sb.AppendLine("Relevant Knowledge:");
                foreach (var chunk in rag)
                    sb.AppendLine($"  - {chunk}");
            }

            if (context.Prompt?.MemoryContext is { Count: > 0 } mem)
            {
                sb.AppendLine();
                sb.AppendLine("Memory:");
                foreach (var entry in mem)
                    sb.AppendLine($"  - {entry}");
            }

            return sb.ToString();
        }

        // ── Response parsing ─────────────────────────────────────────────────────

        protected virtual Plan ParsePlan(IExecutionContext context, string llmOutput)
        {
            try
            {
                var json = ExtractJson(llmOutput);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("steps", out var stepsEl))
                    throw new InvalidOperationException("LLM response missing 'steps' property.");

                var steps = new List<PlanStep>();
                foreach (var stepEl in stepsEl.EnumerateArray())
                {
                    var mode = ExecutionMode.Sequential;
                    if (stepEl.TryGetProperty("mode", out var modeEl) &&
                        Enum.TryParse<ExecutionMode>(modeEl.GetString(), true, out var parsed))
                        mode = parsed;

                    Dictionary<string, object>? inputs = null;
                    if (stepEl.TryGetProperty("inputs", out var inputsEl) &&
                        inputsEl.ValueKind == JsonValueKind.Object)
                    {
                        inputs = [];
                        foreach (var kv in inputsEl.EnumerateObject())
                            inputs[kv.Name] = kv.Value.ToString();
                    }

                    var isHighRisk  = stepEl.TryGetProperty("isHighRisk",  out var hrEl) && hrEl.ValueKind == JsonValueKind.True;
                    var isLoopStart = stepEl.TryGetProperty("isLoopStart", out var lsEl) && lsEl.ValueKind == JsonValueKind.True;
                    var isLoopEnd   = stepEl.TryGetProperty("isLoopEnd",   out var leEl) && leEl.ValueKind == JsonValueKind.True;

                    steps.Add(new PlanStep
                    {
                        Name          = GetString(stepEl, "name"),
                        Description   = GetString(stepEl, "description"),
                        ToolId        = GetStringOrNull(stepEl, "toolId"),
                        AgentId       = GetStringOrNull(stepEl, "agentId"),
                        Inputs        = inputs,
                        Mode          = mode,
                        Priority      = stepEl.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 1,
                        IsHighRisk    = isHighRisk,
                        Condition     = GetStringOrNull(stepEl, "condition"),
                        ElseStepId    = GetStringOrNull(stepEl, "elseStepId"),
                        IsLoopStart   = isLoopStart,
                        IsLoopEnd     = isLoopEnd,
                        LoopCondition = GetStringOrNull(stepEl, "loopCondition"),
                        LoopStartId   = GetStringOrNull(stepEl, "loopStartId"),
                    });
                }

                return new Plan { ContextId = context.Id, Steps = steps, Determinism = Determinism };
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"[LlmPlanner] Failed to parse LLM response for context '{context.Id}': {ex.Message}\nRaw: {llmOutput}",
                    ex);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string ExtractJson(string text)
        {
            // Strip markdown code fences that LLMs produce despite instructions not to.
            var stripped = text;
            var fenceStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (fenceStart >= 0)
            {
                var contentStart = text.IndexOf('\n', fenceStart);
                if (contentStart >= 0)
                {
                    contentStart++;
                    var fenceEnd = text.IndexOf("```", contentStart, StringComparison.Ordinal);
                    if (fenceEnd > contentStart)
                        stripped = text[contentStart..fenceEnd].Trim();
                }
            }

            // Walk each '{' and find the first one that closes a balanced, parseable JSON object.
            // This handles preamble text like "Here is the plan {draft}:\n{...actual json...}".
            for (var i = 0; i < stripped.Length; i++)
            {
                if (stripped[i] != '{') continue;

                var depth = 0;
                var inString = false;
                var escaped = false;

                for (var j = i; j < stripped.Length; j++)
                {
                    var c = stripped[j];

                    if (escaped)             { escaped = false; continue; }
                    if (c == '\\' && inString) { escaped = true;  continue; }
                    if (c == '"')            { inString = !inString; continue; }
                    if (inString)            continue;

                    if      (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            var candidate = stripped[i..(j + 1)];
                            try   { JsonDocument.Parse(candidate); return candidate; }
                            catch (JsonException) { break; } // try next '{'
                        }
                    }
                }
            }

            return stripped; // no valid JSON found — let ParsePlan's catch produce a clear error
        }

        private static string GetString(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static string? GetStringOrNull(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString()
                : null;
    }

    /// <summary>No-op logger used when no ILogger is injected.</summary>
    internal sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public Task LogAsync(LogLevel level, string message, string? correlationId = null,
            System.Diagnostics.Activity? activity = null, IDictionary<string, object>? metadata = null,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
