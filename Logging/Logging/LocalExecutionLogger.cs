namespace Valaiorp.Tools.Enhanced.Logging
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Planner.Models;

    public sealed class LocalExecutionLogger : IExecutionLogger
    {
        private readonly IShortTermMemory _shortTermMemory;
        private readonly string _logKeyPrefix = "execution_log_";

        public LocalExecutionLogger(IShortTermMemory shortTermMemory)
        {
            _shortTermMemory = shortTermMemory;
        }

        public async Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Plan",
                ContextId = context.Id,
                PlanId = plan.Id,
                Timestamp = DateTimeOffset.UtcNow,
                AiUsed = plan.PlanningTokens != null,
                PlanningTokens = plan.PlanningTokens != null ? new
                {
                    plan.PlanningTokens.InputTokens,
                    plan.PlanningTokens.OutputTokens,
                    plan.PlanningTokens.TotalTokens,
                    plan.PlanningTokens.ModelId
                } : null,
                Steps = plan.Steps.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.ToolId,
                    s.ModuleId,
                    SubStepsCount = s.SubSteps.Count
                })
            };

            var logKey = $"{_logKeyPrefix}plan_{plan.Id}";
            await _shortTermMemory.SetAsync(logKey, logEntry, ct).ConfigureAwait(false);
        }

        public async Task LogRunAsync(ExecutionUnit unit, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Run",
                ContextId = context.Id,
                ExecutionId = unit.Id,
                Timestamp = DateTimeOffset.UtcNow,
                Status = unit.Status.ToString(),
                PlanId = unit.Plan.Id,
                StepsCount = unit.Plan.Steps.Count,
                StartedAt = unit.StartedAt,
                CompletedAt = unit.CompletedAt,
                Error = unit.Exception?.Message
            };

            var logKey = $"{_logKeyPrefix}run_{unit.Id}";
            await _shortTermMemory.SetAsync(logKey, logEntry, ct).ConfigureAwait(false);
        }

        public async Task LogStepAsync(TaskNode node, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Step",
                ContextId = context.Id,
                StepId = node.Id,
                StepName = node.Step.Name,
                Timestamp = DateTimeOffset.UtcNow,
                Status = node.Status.ToString(),
                AiUsed = node.AiUsed,
                LlmTokens = node.LlmTokens != null ? new
                {
                    node.LlmTokens.InputTokens,
                    node.LlmTokens.OutputTokens,
                    node.LlmTokens.TotalTokens,
                    node.LlmTokens.ModelId
                } : null,
                Error = node.Exception?.Message,
                Result = node.Result != null ? new
                {
                    node.Result.IsSuccess,
                    node.Result.ErrorMessage,
                    Outputs = node.Result.Outputs
                } : null
            };

            var logKey = $"{_logKeyPrefix}step_{node.Id}";
            await _shortTermMemory.SetAsync(logKey, logEntry, ct).ConfigureAwait(false);
        }
    }
}