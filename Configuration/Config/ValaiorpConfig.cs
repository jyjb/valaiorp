namespace Valaiorp.Configuration.Config
{
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Enums;

    public sealed class AgenticAIConfig
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
        /// Manual overrides set after this call take precedence.
        /// </summary>
        public AgenticAIConfig ApplyProfile()
        {
            var (plannerType, autonomy) = WorkflowProfile.For(WorkflowType, AiParticipation);
            Planner.Type = plannerType;
            Autonomy     = autonomy;
            return this;
        }
    }
}
