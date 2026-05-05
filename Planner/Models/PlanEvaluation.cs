namespace Valaiorp.Planner.Models
{
    public enum EvaluationRecommendation { Proceed, Review, Reject }

    public sealed class PlanEvaluation
    {
        /// <summary>0.0 (no confidence) to 1.0 (full confidence).</summary>
        public double ConfidenceScore { get; init; }

        /// <summary>True when no blocking issues were found.</summary>
        public bool IsValid { get; init; }

        public EvaluationRecommendation Recommendation { get; init; }

        /// <summary>Blocking problems that should prevent execution (or trigger Review).</summary>
        public IReadOnlyList<string> Issues { get; init; } = [];

        /// <summary>Non-blocking observations that may reduce confidence.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
