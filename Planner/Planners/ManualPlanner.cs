namespace Valaiorp.Planner.Planners
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;

    /// <summary>
    /// Executes a plan supplied directly as a Plan object or as a JSON string.
    /// No LLM, no code-authored planner — the caller owns the plan entirely.
    ///
    /// Use cases:
    ///   • Developer testing without an LLM
    ///   • Replaying a logged plan from a previous run
    ///   • Accepting an LLM-generated plan JSON from an external system
    ///   • Integration tests with deterministic, file-based plans
    /// </summary>
    public sealed class ManualPlanner : IPlanner
    {
        private readonly Plan _plan;

        public ManualPlanner(Plan plan)          => _plan = plan;
        public ManualPlanner(string planJson)    => _plan = Deserialize(planJson);
        public ManualPlanner(string planJsonPath, bool isFilePath)
            => _plan = Deserialize(File.ReadAllText(planJsonPath));

        public string            Id          => "manual";
        public PlannerType       Type        => PlannerType.Manual;
        public DeterminismLevel  Determinism { get; set; } = DeterminismLevel.Deterministic;

        public Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            // Stamp the context ID so logging links correctly
            _plan.ContextId = context.Id;
            return Task.FromResult(_plan);
        }

        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private static Plan Deserialize(string json)
            => JsonSerializer.Deserialize<Plan>(json, _opts)
               ?? throw new InvalidOperationException("Plan JSON could not be deserialized.");
    }
}
