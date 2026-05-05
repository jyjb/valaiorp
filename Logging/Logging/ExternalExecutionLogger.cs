namespace Valaiorp.Tools.Enhanced.Logging
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;

    public sealed class ExternalExecutionLogger : IExecutionLogger
    {
        private readonly string _logDirectory;

        public ExternalExecutionLogger(string logDirectory = "logs")
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        public async Task LogPlanAsync(Plan plan, IExecutionContext context, CancellationToken ct = default)
        {
            var plannerType = (plan.Planner?.Type ?? plan.PlanningTokens switch
            {
                null => Core.Enums.PlannerType.Reactive,
                _    => Core.Enums.PlannerType.LlmBased
            }).ToString();

            var logEntry = new
            {
                Type = "Plan",
                Version = plan.Version,
                ContextId = context.Id,
                PlanId = plan.Id,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                DeterminismLevel = plan.Determinism.ToString(),
                AutonomyLevel = plan.AutonomyLevel,
                Planner = new
                {
                    Type = plannerType,
                    PlanningTokens = plan.PlanningTokens != null ? new
                    {
                        plan.PlanningTokens.InputTokens,
                        plan.PlanningTokens.OutputTokens,
                        plan.PlanningTokens.TotalTokens,
                        plan.PlanningTokens.ModelId
                    } : null
                },
                Governance = plan.Governance,
                Metadata = plan.Metadata,
                Steps = plan.Steps.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Type,
                    s.ToolId,
                    s.ModuleId,
                    s.AgentId,
                    ExecutionMode = s.Mode.ToString(),
                    DeterminismLevel = s.Determinism.ToString(),
                    s.Inputs,
                    s.ExpectedOutputs,
                    s.ErrorHandling,
                    s.Validation,
                    s.Observability,
                    s.NextSteps,
                    SubStepsCount = s.SubSteps.Count
                }),
                WorkflowState = plan.WorkflowState.Count > 0 ? plan.WorkflowState : null,
                Dependencies = plan.Dependencies
            };

            var logFilePath = Path.Combine(_logDirectory, $"run_{context.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
            await File.AppendAllTextAsync(logFilePath, logLine, ct).ConfigureAwait(false);
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

            var logFilePath = Path.Combine(_logDirectory, $"run_{context.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
            await File.AppendAllTextAsync(logFilePath, logLine, ct).ConfigureAwait(false);
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

            var logFilePath = Path.Combine(_logDirectory, $"run_{context.Id}.jsonl");
            var logLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
            await File.AppendAllTextAsync(logFilePath, logLine, ct).ConfigureAwait(false);
        }
    }
}
