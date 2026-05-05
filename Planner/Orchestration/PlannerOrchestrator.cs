namespace Valaiorp.Planner.Orchestration
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Planner.Contracts;
    using Valaiorp.Planner.Models;

    public sealed class PlannerOrchestrator
    {
        private readonly ConcurrentDictionary<string, IPlanner> _planners = new();
        private string _defaultPlannerId = string.Empty;
        private readonly IPlanEvaluator? _evaluator;

        public PlannerOrchestrator() { }
        public PlannerOrchestrator(IPlanEvaluator evaluator) { _evaluator = evaluator; }

        public void RegisterPlanner(IPlanner planner, bool setAsDefault = false)
        {
            _planners.TryAdd(planner.Id, planner);
            if (setAsDefault || string.IsNullOrEmpty(_defaultPlannerId))
            {
                _defaultPlannerId = planner.Id;
            }
        }

        public bool TryGetPlanner(string plannerId, out IPlanner? planner)
        {
            return _planners.TryGetValue(plannerId, out planner);
        }

        public async Task<Plan> CreatePlanAsync(
            string? plannerId = null,
            IExecutionContext? context = null,
            CancellationToken ct = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var planner = string.IsNullOrEmpty(plannerId)
                ? GetDefaultPlanner()
                : TryGetPlanner(plannerId, out var p) ? p : null;

            if (planner == null)
            {
                throw new InvalidOperationException("No planner available.");
            }

            try
            {
                var plan = await planner.CreatePlanAsync(context, ct).ConfigureAwait(false);
                if (_evaluator != null)
                    plan.Evaluation = _evaluator.Evaluate(plan);
                return plan;
            }
            catch
            {
                // Fallback to default planner if available
                var defaultPlanner = GetDefaultPlanner();
                if (defaultPlanner != null && defaultPlanner.Id != planner.Id)
                {
                    var fallbackPlan = await defaultPlanner.CreatePlanAsync(context, ct).ConfigureAwait(false);
                    if (_evaluator != null)
                        fallbackPlan.Evaluation = _evaluator.Evaluate(fallbackPlan);
                    return fallbackPlan;
                }
                throw;
            }
        }

        public IPlanner? GetDefaultPlanner()
        {
            if (string.IsNullOrEmpty(_defaultPlannerId))
            {
                return _planners.Values.FirstOrDefault();
            }
            return _planners.TryGetValue(_defaultPlannerId, out var planner) ? planner : null;
        }

        public void SetDefaultPlanner(string plannerId)
        {
            if (_planners.ContainsKey(plannerId))
            {
                _defaultPlannerId = plannerId;
            }
        }
    }
}