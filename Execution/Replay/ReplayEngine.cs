namespace Valaiorp.Execution.Replay
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;

    /// <summary>
    /// Captures per-step state during a workflow run and enables deterministic re-runs
    /// from any prior checkpoint (time-travel debugging).
    ///
    /// Usage — capture:
    ///   Pass a ReplayEngine to WorkflowExecutor.ExecuteAsync; snapshots accumulate
    ///   automatically. Call BuildSnapshot() after execution to persist the result.
    ///
    /// Usage — replay:
    ///   Given a saved ExecutionSnapshot, call RestoreState to seed WorkflowState,
    ///   then call GetReplaySteps to get the tail of the step list, then re-execute.
    /// </summary>
    public sealed class ReplayEngine
    {
        private readonly List<StepSnapshot> _snapshots = [];
        private readonly string _contextId;
        private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

        public ReplayEngine(string contextId) => _contextId = contextId;

        // ── Capture (called by WorkflowExecutor) ────────────────────────────────

        internal void Capture(
            WorkflowStep step,
            WorkflowExecutionContext context,
            IExecutionResult result,
            TimeSpan duration)
        {
            _snapshots.Add(new StepSnapshot
            {
                StepId    = step.Id,
                StepName  = step.Name,
                StepIndex = _snapshots.Count,
                Input     = step.Input,
                Output    = result.IsSuccess
                    ? result.Outputs.FirstOrDefault().Value?.ToString()
                    : null,
                Error                 = result.ErrorMessage,
                IsSuccess             = result.IsSuccess,
                Duration              = duration,
                CapturedAt            = DateTimeOffset.UtcNow,
                WorkflowStateSnapshot = new Dictionary<string, object>(context.WorkflowState)
            });
        }

        public ExecutionSnapshot BuildSnapshot() => new()
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            ContextId   = _contextId,
            StartedAt   = _startedAt,
            Steps       = _snapshots.AsReadOnly()
        };

        // ── Restore / replay helpers (static) ───────────────────────────────────

        /// <summary>
        /// Restores <paramref name="context"/>.WorkflowState to the state that existed
        /// immediately after snapshot step <paramref name="stepIndex"/> - 1 completed.
        /// Pass 0 to start a clean replay from the very beginning.
        /// </summary>
        public static void RestoreState(
            ExecutionSnapshot snapshot,
            int stepIndex,
            WorkflowExecutionContext context)
        {
            context.WorkflowState.Clear();

            if (stepIndex <= 0)
                return;

            var source = snapshot.GetByIndex(stepIndex - 1);
            if (source == null)
                return;

            foreach (var kv in source.WorkflowStateSnapshot)
                context.WorkflowState[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Returns the sub-list of <paramref name="allSteps"/> starting at
        /// <paramref name="fromIndex"/> so the executor only re-runs the remaining steps.
        /// </summary>
        public static IReadOnlyList<WorkflowStep> GetReplaySteps(
            IReadOnlyList<WorkflowStep> allSteps,
            int fromIndex)
            => fromIndex <= 0
                ? allSteps
                : allSteps.Skip(fromIndex).ToList();
    }
}
