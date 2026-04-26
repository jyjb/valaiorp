namespace Valaiorp.Planner.Planners
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Registries;

    public sealed class InternalPlanner : IPlanner
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly ModuleRegistry _moduleRegistry;

        public InternalPlanner(ToolRegistry toolRegistry, ModuleRegistry moduleRegistry)
        {
            _toolRegistry = toolRegistry;
            _moduleRegistry = moduleRegistry;
            Id = nameof(InternalPlanner);
            Type = PlannerType.Reactive;
        }

        public string Id { get; }
        public PlannerType Type { get; }
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;

        public async Task<Plan> CreatePlanAsync(IExecutionContext context, CancellationToken ct = default)
        {
            var steps = new List<PlanStep>();

            // Example: Create a plan based on available tools/modules
            foreach (var tool in _toolRegistry.Tools.Values)
            {
                steps.Add(new PlanStep
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ToolId = tool.Id,
                    Mode = ExecutionMode.Sequential,
                    Priority = 1
                });
            }

            foreach (var module in _moduleRegistry.Modules.Values)
            {
                foreach (var tool in module.Tools)
                {
                    steps.Add(new PlanStep
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        ToolId = tool.Id,
                        ModuleId = module.Id,
                        Mode = ExecutionMode.Sequential,
                        Priority = 2
                    });
                }
            }

            return new Plan
            {
                ContextId = context.Id,
                Steps = steps,
                Determinism = Determinism
            };
        }
    }
}