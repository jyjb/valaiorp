namespace Valaiorp.Tools.Enhanced.Logging
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;
    using Valaiorp.BasicTools.FileTools;

    public sealed class ExternalExecutionLogger : IExecutionLogger
    {
        private readonly JsonlTool _jsonlTool;
        private readonly string _logDirectory;

        public ExternalExecutionLogger(string logDirectory = "logs")
        {
            _jsonlTool = new JsonlTool();
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        public async Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Plan",
                ContextId = context.Id,
                PlanId = plan.Id,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
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

            var logFilePath = Path.Combine(_logDirectory, $"plan_{plan.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry);
            await _jsonlTool.WriteAsync(logFilePath, logLine, ct).ConfigureAwait(false);
        }

        public async Task LogRunAsync(ExecutionUnit unit, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Run",
                ContextId = context.Id,
                ExecutionId = unit.Id,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Status = unit.Status.ToString(),
                PlanId = unit.Plan.Id,
                StepsCount = unit.Plan.Steps.Count,
                StartedAt = unit.StartedAt.ToString("o"),
                CompletedAt = unit.CompletedAt?.ToString("o"),
                Error = unit.Exception?.Message
            };

            var logFilePath = Path.Combine(_logDirectory, $"run_{unit.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry);
            await _jsonlTool.WriteAsync(logFilePath, logLine, ct).ConfigureAwait(false);
        }

        public async Task LogStepAsync(TaskNode node, IExecutionContext context, CancellationToken ct = default)
        {
            var logEntry = new
            {
                Type = "Step",
                ContextId = context.Id,
                StepId = node.Id,
                StepName = node.Step.Name,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
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

            var logFilePath = Path.Combine(_logDirectory, $"step_{node.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry);
            await _jsonlTool.WriteAsync(logFilePath, logLine, ct).ConfigureAwait(false);
        }
    }
}
