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
                return await planner.CreatePlanAsync(context, ct).ConfigureAwait(false);
            }
            catch
            {
                // Fallback to default planner if available
                var defaultPlanner = GetDefaultPlanner();
                if (defaultPlanner != null && defaultPlanner.Id != planner.Id)
                {
                    return await defaultPlanner.CreatePlanAsync(context, ct).ConfigureAwait(false);
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