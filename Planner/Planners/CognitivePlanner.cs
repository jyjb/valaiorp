namespace Valaiorp.Planner.Planners
{
    using System.Text;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Registries;

    /// <summary>
    /// Deliberative planner that runs three LLM passes: initial plan → self-critique → revision.
    /// Produces higher-quality plans at the cost of 2-3x more LLM calls.
    /// Inherits prompt construction and JSON parsing from LlmPlanner.
    /// </summary>
    public class CognitivePlanner : LlmPlanner
    {
        private readonly ILlmClient _llmClient;

        public CognitivePlanner(ILlmClient llmClient, ToolRegistry toolRegistry, ModuleRegistry moduleRegistry)
            : base(llmClient, toolRegistry, moduleRegistry)
        {
            _llmClient = llmClient;
            Id = nameof(CognitivePlanner);
            Type = PlannerType.Deliberative;
            Determinism = DeterminismLevel.SemiDeterministic;
        }

        public override async Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            // Pass 1 — initial plan
            var initialPlan = await base.CreatePlanAsync(context, ct).ConfigureAwait(false);
            if (initialPlan.Steps.Count == 0)
                return initialPlan;

            // Pass 2 — critique
            var critiquePrompt = BuildCritiquePrompt(context, initialPlan);
            var critiqueResponse = await _llmClient.CompleteAsync(critiquePrompt, ct).ConfigureAwait(false);
            if (!critiqueResponse.IsSuccess)
                return initialPlan;

            // Pass 3 — revised plan
            var revisionPrompt = BuildRevisionPrompt(context, initialPlan, critiqueResponse.Content);
            var revisionResponse = await _llmClient.CompleteAsync(revisionPrompt, ct).ConfigureAwait(false);
            return revisionResponse.IsSuccess
                ? ParsePlan(context, revisionResponse.Content)
                : initialPlan;
        }

        // ── Critique prompt ──────────────────────────────────────────────────────

        private PromptContext BuildCritiquePrompt(IExecutionContext context, Plan plan)
        {
            var planSummary = SummarisePlan(plan);
            return new PromptContext
            {
                SystemPrompt =
                    "You are a critical reviewer. Identify flaws, missing steps, incorrect tool usage, " +
                    "or ordering issues in the execution plan below. " +
                    "Respond with a concise list of specific improvements only — no JSON.",
                UserPrompt =
                    $"Goal: {context.Prompt?.UserPrompt ?? string.Empty}\n\nPlan:\n{planSummary}",
                ConversationHistory = context.Prompt?.ConversationHistory ?? [],
                Variables = context.Prompt?.Variables ?? new Dictionary<string, string>()
            };
        }

        // ── Revision prompt ──────────────────────────────────────────────────────

        private PromptContext BuildRevisionPrompt(IExecutionContext context, Plan plan, string critique)
        {
            var planSummary = SummarisePlan(plan);
            var sb = new StringBuilder();
            sb.AppendLine("You are a planning agent. Revise the plan below based on the critique provided.");
            sb.AppendLine("Return ONLY a JSON object — no markdown fences — using the same schema as before:");
            sb.AppendLine("""{"steps":[{"name":"...","description":"...","toolId":"...","agentId":null,"inputs":{},"mode":"Sequential","priority":1}]}""");

            return new PromptContext
            {
                SystemPrompt = sb.ToString(),
                UserPrompt =
                    $"Goal: {context.Prompt?.UserPrompt ?? string.Empty}\n\n" +
                    $"Original Plan:\n{planSummary}\n\n" +
                    $"Critique:\n{critique}\n\n" +
                    "Produce the revised plan as JSON.",
                RagContext = context.Prompt?.RagContext ?? [],
                MemoryContext = context.Prompt?.MemoryContext ?? [],
                ConversationHistory = context.Prompt?.ConversationHistory ?? [],
                Variables = context.Prompt?.Variables ?? new Dictionary<string, string>()
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string SummarisePlan(Plan plan)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                var s = plan.Steps[i];
                sb.AppendLine($"{i + 1}. [{s.Mode}] {s.Name}: {s.Description}" +
                              (s.ToolId != null ? $" (tool: {s.ToolId})" : "") +
                              (s.AgentId != null ? $" (agent: {s.AgentId})" : ""));
            }
            return sb.ToString();
        }
    }
}
