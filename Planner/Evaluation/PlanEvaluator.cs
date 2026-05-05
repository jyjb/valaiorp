namespace Valaiorp.Planner.Evaluation
{
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;
    using Valaiorp.Tools.Registries;

    /// <summary>
    /// Scores a plan for confidence and validates structural correctness before execution.
    ///
    /// Confidence deductions:
    ///   - Step with no name                     : -0.05 per step
    ///   - Step with no toolId/agentId/moduleId  : -0.05 per step (warning, not issue)
    ///   - Step referencing unregistered tool    : -0.15 per step (requires ToolRegistry)
    ///   - Loop-end with unknown loopStartId     : -0.10 per step
    ///   - Governance risk score                 : -(RiskScore × 0.1)
    ///
    /// Recommendation thresholds:
    ///   >= 0.70 → Proceed  |  >= 0.40 → Review  |  below → Reject
    /// </summary>
    public sealed class PlanEvaluator : IPlanEvaluator
    {
        private readonly ToolRegistry? _toolRegistry;

        public PlanEvaluator(ToolRegistry? toolRegistry = null)
            => _toolRegistry = toolRegistry;

        public PlanEvaluation Evaluate(Plan plan)
        {
            var issues   = new List<string>();
            var warnings = new List<string>();
            var confidence = 1.0;

            if (plan.Steps.Count == 0)
            {
                return new PlanEvaluation
                {
                    ConfidenceScore = 0.0,
                    IsValid         = false,
                    Recommendation  = EvaluationRecommendation.Reject,
                    Issues          = ["Plan has no steps."]
                };
            }

            var loopStartNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in plan.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    issues.Add($"Step '{step.Id}' is missing a name.");
                    confidence -= 0.05;
                    continue;
                }

                if (step.IsLoopStart)
                    loopStartNames.Add(step.Name);

                var hasTarget = !string.IsNullOrEmpty(step.ToolId)
                             || !string.IsNullOrEmpty(step.AgentId)
                             || !string.IsNullOrEmpty(step.ModuleId);

                if (!hasTarget)
                {
                    warnings.Add($"Step '{step.Name}' has no toolId, agentId, or moduleId.");
                    confidence -= 0.05;
                }
                else if (_toolRegistry != null
                    && !string.IsNullOrEmpty(step.ToolId)
                    && !_toolRegistry.Tools.ContainsKey(step.ToolId))
                {
                    issues.Add($"Step '{step.Name}' references unregistered tool '{step.ToolId}'.");
                    confidence -= 0.15;
                }

                if (step.IsLoopEnd
                    && !string.IsNullOrEmpty(step.LoopStartId)
                    && !loopStartNames.Contains(step.LoopStartId))
                {
                    issues.Add($"Loop-end step '{step.Name}' references unknown loopStartId '{step.LoopStartId}'.");
                    confidence -= 0.10;
                }
            }

            if (plan.Governance?.RiskScore > 0)
                confidence -= plan.Governance.RiskScore * 0.1;

            confidence = Math.Clamp(confidence, 0.0, 1.0);

            var recommendation = confidence >= 0.70
                ? EvaluationRecommendation.Proceed
                : confidence >= 0.40
                    ? EvaluationRecommendation.Review
                    : EvaluationRecommendation.Reject;

            return new PlanEvaluation
            {
                ConfidenceScore = Math.Round(confidence, 4),
                IsValid         = issues.Count == 0,
                Recommendation  = recommendation,
                Issues          = issues,
                Warnings        = warnings
            };
        }
    }
}
