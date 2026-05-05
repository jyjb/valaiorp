namespace Valaiorp.Planner.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class Plan
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Version { get; set; } = "1.0";
        public string ContextId { get; set; } = string.Empty;
        public IReadOnlyList<PlanStep> Steps { get; set; } = Array.Empty<PlanStep>();
        public DeterminismLevel Determinism { get; set; }
        public double AutonomyLevel { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public TokenUsage? PlanningTokens { get; set; }
        public PlannerInfo? Planner { get; set; }
        public PlanGovernance? Governance { get; set; }
        public PlanMetadata? Metadata { get; set; }
        public IDictionary<string, object?> WorkflowState { get; set; } = new Dictionary<string, object?>();
        public PlanDependencies? Dependencies { get; set; }
        public PlanEvaluation? Evaluation { get; set; }
    }

    public sealed class PlanStep
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "Tool";
        public string? ToolId { get; set; }
        public string? ModuleId { get; set; }

        /// <summary>
        /// Target agent ID for multi-agent delegation.
        /// When set, the MultiAgentOrchestrator dispatches this step as an AgentMessage
        /// to the named agent instead of executing it with a local tool.
        /// </summary>
        public string? AgentId { get; set; }

        public IReadOnlyDictionary<string, object>? Inputs { get; set; }
        public IReadOnlyDictionary<string, object>? ExpectedOutputs { get; set; }
        public IReadOnlyList<PlanStep> SubSteps { get; set; } = Array.Empty<PlanStep>();
        public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
        public DeterminismLevel Determinism { get; set; } = DeterminismLevel.Deterministic;
        public int Priority { get; set; }
        public StepErrorHandling? ErrorHandling { get; set; }
        public StepValidation? Validation { get; set; }
        public StepObservability? Observability { get; set; }
        public StepNextSteps? NextSteps { get; set; }
        public IReadOnlyDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// When true the executor must obtain approval via IEscalationService before running this step.
        /// Set by the LLM planner for steps that write data, call external APIs, send messages, or delete anything.
        /// </summary>
        public bool IsHighRisk { get; set; }

        // ── Conditional execution ──────────────��──────────────────────────────────

        /// <summary>
        /// Expression evaluated before this step runs. Step is skipped when false.
        /// Syntax: WorkflowState['key'] == value  |  != value  |  > N  |  &lt; N  |  CONTAINS substring
        /// </summary>
        public string? Condition { get; set; }

        /// <summary>
        /// Name of the step to route to when <see cref="Condition"/> evaluates false.
        /// If omitted and condition is false the step is simply skipped.
        /// </summary>
        public string? ElseStepId { get; set; }

        // ── Loop control ──────────────────────────────────────────────────────────

        /// <summary>Marks this step as the start of a repeating loop body.</summary>
        public bool IsLoopStart { get; set; }

        /// <summary>
        /// Marks this step as the end of a loop body. When <see cref="LoopCondition"/>
        /// is still true after this step the executor jumps back to the step after the
        /// matching loop-start and increments WorkflowState['iteration'].
        /// </summary>
        public bool IsLoopEnd { get; set; }

        /// <summary>
        /// Condition re-evaluated after each loop iteration.
        /// Syntax: "iteration &lt; N"  |  "iteration &lt;= N"  |  WorkflowState expression.
        /// Loop stops when this evaluates false (or after 1000 iterations).
        /// </summary>
        public string? LoopCondition { get; set; }

        /// <summary>
        /// Name of the corresponding <see cref="IsLoopStart"/> step.
        /// Required on loop-end steps so the executor can locate the loop body.
        /// </summary>
        public string? LoopStartId { get; set; }
    }
}
