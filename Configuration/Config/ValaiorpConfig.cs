namespace Valaiorp.Configuration.Config
{
    using System.Text;
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Enums;

    public sealed class ValaiorpConfig
    {
        // ── Workflow identity — set these two; call ApplyProfile() to wire the rest ──

        /// <summary>The type of workflow: Irpa, AiWorkflow, AiAgent, or Agentic.</summary>
        public WorkflowType WorkflowType { get; set; } = WorkflowType.Irpa;

        /// <summary>How much the AI is allowed to act. Ignored for Irpa workflows.</summary>
        public AiParticipation AiParticipation { get; set; } = AiParticipation.ObserveAndReact;

        // ── Fine-grained config — populated by ApplyProfile() or set manually ────

        public ExecutionConfig   Execution   { get; set; } = new();
        public PlannerConfig     Planner     { get; set; } = new();
        public LlmConfig         Llm         { get; set; } = new();
        public KnowledgeConfig   Knowledge   { get; set; } = new();
        public ParallelismConfig Parallelism { get; set; } = new();
        public GovernanceConfig  Governance  { get; set; } = new();
        public AutonomyConfig    Autonomy    { get; set; } = new();
        public GuardrailConfig   Guardrails  { get; set; } = new();
        public PersistenceConfig Persistence { get; set; } = new();

        /// <summary>
        /// Applies WorkflowType + AiParticipation presets, filling Planner.Type and Autonomy
        /// with the right values for the chosen workflow profile.
        ///
        /// Fields you explicitly changed from their defaults in valaiorp.json are preserved:
        /// the method compares each Autonomy field against the out-of-box defaults and re-applies
        /// anything that was explicitly overridden. To unconditionally use the profile's Autonomy,
        /// set Autonomy to <c>new AutonomyConfig()</c> before calling this method.
        /// </summary>
        /// <summary>
        /// Validates the configuration and throws <see cref="InvalidOperationException"/>
        /// listing all violations if any are found. Call this at startup before registering services.
        /// </summary>
        public ValaiorpConfig Validate()
        {
            var errors = new List<string>();

            // ExecutionConfig
            if (Execution.Timeout <= TimeSpan.Zero)
                errors.Add($"Execution.Timeout must be positive (got {Execution.Timeout}).");
            if (Execution.MaxRetries < 0)
                errors.Add($"Execution.MaxRetries must be >= 0 (got {Execution.MaxRetries}).");
            if (Execution.EnableCircuitBreaker)
            {
                if (Execution.CircuitBreakerThreshold <= 0)
                    errors.Add($"Execution.CircuitBreakerThreshold must be > 0 when EnableCircuitBreaker is true (got {Execution.CircuitBreakerThreshold}).");
                if (Execution.CircuitBreakerResetTime <= TimeSpan.Zero)
                    errors.Add($"Execution.CircuitBreakerResetTime must be positive when EnableCircuitBreaker is true (got {Execution.CircuitBreakerResetTime}).");
            }

            // PlannerConfig
            if (Planner.MaxDepth <= 0)
                errors.Add($"Planner.MaxDepth must be > 0 (got {Planner.MaxDepth}).");
            if (Planner.MaxBranchingFactor <= 0)
                errors.Add($"Planner.MaxBranchingFactor must be > 0 (got {Planner.MaxBranchingFactor}).");
            if (Planner.PlanningTimeout <= TimeSpan.Zero)
                errors.Add($"Planner.PlanningTimeout must be positive (got {Planner.PlanningTimeout}).");

            // AutonomyConfig
            if (Autonomy.Level < 0.0 || Autonomy.Level > 1.0)
                errors.Add($"Autonomy.Level must be in [0.0, 1.0] (got {Autonomy.Level}).");

            // KnowledgeConfig
            if (Knowledge.MaxContextLength <= 0)
                errors.Add($"Knowledge.MaxContextLength must be > 0 (got {Knowledge.MaxContextLength}).");
            if (Knowledge.MaxKnowledgeResults <= 0)
                errors.Add($"Knowledge.MaxKnowledgeResults must be > 0 (got {Knowledge.MaxKnowledgeResults}).");
            if (Knowledge.SimilarityThreshold < 0.0f || Knowledge.SimilarityThreshold > 1.0f)
                errors.Add($"Knowledge.SimilarityThreshold must be in [0.0, 1.0] (got {Knowledge.SimilarityThreshold}).");

            // ParallelismConfig
            if (Parallelism.MaxDegreeOfParallelism <= 0)
                errors.Add($"Parallelism.MaxDegreeOfParallelism must be > 0 (got {Parallelism.MaxDegreeOfParallelism}).");
            if (Parallelism.MaxConcurrentExecutions <= 0)
                errors.Add($"Parallelism.MaxConcurrentExecutions must be > 0 (got {Parallelism.MaxConcurrentExecutions}).");
            if (Parallelism.MinThreadPoolSize > Parallelism.MaxThreadPoolSize)
                errors.Add($"Parallelism.MinThreadPoolSize ({Parallelism.MinThreadPoolSize}) must be <= MaxThreadPoolSize ({Parallelism.MaxThreadPoolSize}).");

            if (errors.Count > 0)
            {
                var sb = new StringBuilder("Invalid ValaiorpConfig:");
                foreach (var e in errors)
                    sb.Append("\n  - ").Append(e);
                throw new InvalidOperationException(sb.ToString());
            }

            return this;
        }

        public ValaiorpConfig ApplyProfile()
        {
            // Capture what the user explicitly set before the profile overwrites.
            var userAutonomy = Autonomy;
            var sentinel     = new AutonomyConfig(); // factory defaults used for comparison

            var (plannerType, profileAutonomy) = WorkflowProfile.For(WorkflowType, AiParticipation);
            Planner.Type = plannerType;
            Autonomy     = profileAutonomy;

            // Re-apply any field the user changed from its default.
            // Fields still at their default value are left as the profile dictates.
            if (userAutonomy.Level != sentinel.Level)
                Autonomy.Level = userAutonomy.Level;
            if (userAutonomy.AllowDynamicPlanning != sentinel.AllowDynamicPlanning)
                Autonomy.AllowDynamicPlanning = userAutonomy.AllowDynamicPlanning;
            if (userAutonomy.AllowToolSelection != sentinel.AllowToolSelection)
                Autonomy.AllowToolSelection = userAutonomy.AllowToolSelection;
            if (userAutonomy.AllowConditionalBranching != sentinel.AllowConditionalBranching)
                Autonomy.AllowConditionalBranching = userAutonomy.AllowConditionalBranching;
            if (userAutonomy.RequireApprovalForHighRisk != sentinel.RequireApprovalForHighRisk)
                Autonomy.RequireApprovalForHighRisk = userAutonomy.RequireApprovalForHighRisk;

            return this;
        }
    }
}
