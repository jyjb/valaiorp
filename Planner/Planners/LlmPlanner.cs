namespace Valaiorp.Planner.Planners
{
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
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

        public LlmPlanner(ILlmClient llmClient, ToolRegistry toolRegistry, ModuleRegistry moduleRegistry)
        {
            _llmClient = llmClient;
            _toolRegistry = toolRegistry;
            _moduleRegistry = moduleRegistry;
            Id = nameof(LlmPlanner);
        }

        public string Id { get; protected set; }
        public PlannerType Type { get; protected set; } = PlannerType.Hierarchical;
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.NonDeterministic;

        public virtual async Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            var promptContext = BuildPrompt(context);
            var response = await _llmClient.CompleteAsync(promptContext, ct).ConfigureAwait(false);
            return response.IsSuccess
                ? ParsePlan(context, response.Content)
                : FallbackPlan(context);
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

            // Base instruction
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
      "priority": 1
    }
  ]
}
""");
            sb.AppendLine("Rules:");
            sb.AppendLine("- Use only toolId values from the Available Tools list below.");
            sb.AppendLine("- Set mode to \"Parallel\" only for steps that are fully independent.");
            sb.AppendLine("- Lower priority numbers execute first (1 = highest).");
            sb.AppendLine("- Set agentId when delegating a sub-task to another agent.");

            // Available tools
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

            // Injected context from custom system prompt template
            if (context.Prompt is { } p && !string.IsNullOrWhiteSpace(p.SystemPrompt))
            {
                sb.AppendLine();
                sb.AppendLine("Additional Instructions:");
                sb.AppendLine(p.SystemPrompt);
            }

            // RAG context
            if (context.Prompt?.RagContext is { Count: > 0 } rag)
            {
                sb.AppendLine();
                sb.AppendLine("Relevant Knowledge:");
                foreach (var chunk in rag)
                    sb.AppendLine($"  - {chunk}");
            }

            // Memory context
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
                    return FallbackPlan(context);

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

                    steps.Add(new PlanStep
                    {
                        Name        = GetString(stepEl, "name"),
                        Description = GetString(stepEl, "description"),
                        ToolId      = GetStringOrNull(stepEl, "toolId"),
                        AgentId     = GetStringOrNull(stepEl, "agentId"),
                        Inputs      = inputs,
                        Mode        = mode,
                        Priority    = stepEl.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 1
                    });
                }

                return new Plan { ContextId = context.Id, Steps = steps, Determinism = Determinism };
            }
            catch
            {
                return FallbackPlan(context);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        protected Plan FallbackPlan(IExecutionContext context)
        {
            var goal = context.Prompt?.UserPrompt ?? context.Id;
            return new Plan
            {
                ContextId = context.Id,
                Determinism = Determinism,
                Steps =
                [
                    new PlanStep
                    {
                        Name        = "Execute Goal",
                        Description = goal,
                        Mode        = ExecutionMode.Sequential,
                        Priority    = 1
                    }
                ]
            };
        }

        private static string ExtractJson(string text)
        {
            // Strip markdown fences if present (```json ... ```)
            var start = text.IndexOf('{');
            var end   = text.LastIndexOf('}');
            return start >= 0 && end > start
                ? text[start..(end + 1)]
                : text;
        }

        private static string GetString(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static string? GetStringOrNull(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString()
                : null;
    }
}
