namespace Valaiorp.Planner.Planners
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Registries;

    /// <summary>
    /// Deterministic fallback planner — no LLM required.
    ///
    /// Algorithm:
    ///   1. Tokenize context.Prompt.UserPrompt into lowercase words (>2 chars).
    ///   2. Score each registered tool/module-tool by counting token hits across
    ///      its Name and Description fields.
    ///   3. Emit only tools with score > 0, ordered by score descending.
    ///   4. When no prompt is present (or no tool matches), emit all tools in
    ///      registration order so the runtime always has steps to execute.
    ///
    /// Module tools are included alongside registry tools; both are scored the same way.
    /// Steps are always sequential — parallelism requires LLM-based intent understanding.
    /// </summary>
    public sealed class InternalPlanner : IPlanner
    {
        private readonly ToolRegistry   _toolRegistry;
        private readonly ModuleRegistry _moduleRegistry;

        public InternalPlanner(ToolRegistry toolRegistry, ModuleRegistry moduleRegistry)
        {
            _toolRegistry   = toolRegistry;
            _moduleRegistry = moduleRegistry;
            Id              = nameof(InternalPlanner);
            Type            = PlannerType.Reactive;
        }

        public string         Id          { get; }
        public PlannerType    Type        { get; }
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;

        public Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            var tokens = Tokenize(context.Prompt?.UserPrompt ?? string.Empty);

            var scored = new List<(PlanStep step, int score)>();

            foreach (var tool in _toolRegistry.Tools.Values)
            {
                var score = Score(tokens, tool.Name, tool.Description);
                scored.Add((MakeStep(tool, moduleId: null), score));
            }

            foreach (var module in _moduleRegistry.Modules.Values)
            {
                foreach (var tool in module.Tools)
                {
                    // Include module name/description in the score — a prompt that mentions
                    // the module is a strong signal for its tools.
                    var score = Score(tokens, tool.Name, tool.Description, module.Name, module.Description);
                    scored.Add((MakeStep(tool, module.Id), score));
                }
            }

            List<PlanStep> steps;

            if (tokens.Length == 0 || scored.All(x => x.score == 0))
            {
                // No prompt or no matches — run everything in registration order.
                steps = scored.Select(x => x.step).ToList();
            }
            else
            {
                // Emit matched tools only, highest score first.
                steps = scored
                    .Where(x => x.score > 0)
                    .OrderByDescending(x => x.score)
                    .Select(x => x.step)
                    .ToList();
            }

            var plan = new Plan
            {
                ContextId   = context.Id,
                Steps       = steps,
                Determinism = Determinism
            };

            return Task.FromResult(plan);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static PlanStep MakeStep(ITool tool, string? moduleId) => new()
        {
            Name        = tool.Name,
            Description = tool.Description,
            ToolId      = tool.Id,
            ModuleId    = moduleId,
            Mode        = ExecutionMode.Sequential,
            Priority    = moduleId == null ? 1 : 2
        };

        /// <summary>
        /// Splits text on whitespace and punctuation, lowercases, deduplicates, and
        /// drops tokens shorter than 3 characters (eliminates most English stop-words).
        /// </summary>
        private static string[] Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            return text
                .Split([' ', '\t', '\n', '\r', ',', '.', '?', '!', ';', ':', '-', '_', '(', ')'],
                       StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length >= 3)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Counts how many query tokens appear in the concatenation of the supplied fields.
        /// Case-insensitive substring match — "read" matches "ReadFile", "reader", etc.
        /// </summary>
        private static int Score(string[] tokens, params string[] fields)
        {
            if (tokens.Length == 0) return 0;

            var haystack = string.Join(" ", fields).ToLowerInvariant();
            return tokens.Count(t => haystack.Contains(t, StringComparison.Ordinal));
        }
    }
}
