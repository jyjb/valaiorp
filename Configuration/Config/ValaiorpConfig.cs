namespace Valaiorp.Configuration.Config
{
    using Valaiorp.Configuration.Models;

    public sealed class AgenticAIConfig
    {
        public ExecutionConfig Execution { get; set; } = new();
        public PlannerConfig Planner { get; set; } = new();
        public LlmConfig Llm { get; set; } = new();
        public KnowledgeConfig Knowledge { get; set; } = new();
        public ParallelismConfig Parallelism { get; set; } = new();
        public GovernanceConfig Governance { get; set; } = new();
        public AutonomyConfig Autonomy { get; set; } = new();

    }
}
