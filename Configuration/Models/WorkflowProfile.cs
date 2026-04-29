namespace Valaiorp.Configuration.Models
{
    using Valaiorp.Core.Enums;

    /// <summary>
    /// Maps (WorkflowType, AiParticipation) to concrete planner and autonomy settings.
    /// Call ValaiorpConfig.ApplyProfile() to apply these presets automatically.
    /// </summary>
    public static class WorkflowProfile
    {
        public static (PlannerType Planner, AutonomyConfig Autonomy) For(
            WorkflowType workflow,
            AiParticipation participation = AiParticipation.ObserveAndReact)
        {
            return (workflow, participation) switch
            {
                // ── IRPA — no AI at all ──────────────────────────────────────────────
                (WorkflowType.Irpa, _) => (
                    PlannerType.Reactive,   // deterministic, no LLM required
                    new AutonomyConfig
                    {
                        Level                    = 0.0,
                        AllowDynamicPlanning     = false,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = false,
                        RequireApprovalForHighRisk = false
                    }),

                // ── AI Workflow ──────────────────────────────────────────────────────
                (WorkflowType.AiWorkflow, AiParticipation.ObserveOnly) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.1,
                        AllowDynamicPlanning     = false,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = false,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.AiWorkflow, AiParticipation.ObserveAndSuggest) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.3,
                        AllowDynamicPlanning     = false,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.AiWorkflow, AiParticipation.ObserveAndReact) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.4,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                // ── AI Agent ─────────────────────────────────────────────────────────
                (WorkflowType.AiAgent, AiParticipation.ObserveOnly) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.4,
                        AllowDynamicPlanning     = false,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.AiAgent, AiParticipation.ObserveAndSuggest) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.6,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.AiAgent, AiParticipation.ObserveAndReact) => (
                    PlannerType.LlmBased,
                    new AutonomyConfig
                    {
                        Level                    = 0.7,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = true,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                // ── Agentic ──────────────────────────────────────────────────────────
                (WorkflowType.Agentic, AiParticipation.ObserveOnly) => (
                    PlannerType.AutonomyAware,
                    new AutonomyConfig
                    {
                        Level                    = 0.7,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = false,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.Agentic, AiParticipation.ObserveAndSuggest) => (
                    PlannerType.AutonomyAware,
                    new AutonomyConfig
                    {
                        Level                    = 0.8,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = true,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = true
                    }),

                (WorkflowType.Agentic, AiParticipation.ObserveAndReact) => (
                    PlannerType.AutonomyAware,
                    new AutonomyConfig
                    {
                        Level                    = 1.0,
                        AllowDynamicPlanning     = true,
                        AllowToolSelection       = true,
                        AllowConditionalBranching = true,
                        RequireApprovalForHighRisk = false
                    }),

                _ => (PlannerType.Deliberative, new AutonomyConfig())
            };
        }
    }
}
