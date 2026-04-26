namespace Valaiorp.Tools.Enhanced.Registries
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.BasicTools.Registries;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Enhanced.Logging;
    using Valaiorp.Tools.Registries;

    public static class EnhancedToolsRegistry
    {
        public static void RegisterAll(
            ToolRegistry toolRegistry,
            IShortTermMemory shortTermMemory,
            string logDirectory = "logs")
        {
            // Register basic tools
            BasicToolsRegistry.RegisterAll(toolRegistry);

            // Register loggers as tools
            var localLogger = new LocalExecutionLogger(shortTermMemory);
            var externalLogger = new ExternalExecutionLogger(logDirectory);

            toolRegistry.Register(new LoggerTool("local-logger", localLogger));
            toolRegistry.Register(new LoggerTool("external-logger", externalLogger));
        }
    }

    // Wrapper to expose loggers as tools
    public sealed class LoggerTool : ITool
    {
        private readonly IExecutionLogger _logger;

        public LoggerTool(string id, IExecutionLogger logger)
        {
            Id = id;
            _logger = logger;
            Name = $"{id} Tool";
            Description = $"Logs execution details using {id}.";
            Type = ToolType.Native;
            Metadata = new Dictionary<string, object>
            {
                { "LoggerType", id }
            };
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public ToolType Type { get; }
        public IReadOnlyDictionary<string, object> Metadata { get; }

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            string input,
            CancellationToken ct = default)
        {
            try
            {
                var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return ToolResult.BadRequest("Invalid input format. Use: log|plan|run|step|<id>");

                var logType = parts[1].Trim().ToLower();
                var targetId = parts.Length > 2 ? parts[2].Trim() : string.Empty;

                switch (logType)
                {
                    case "plan":
                        if (context.Metadata.TryGetValue("CurrentPlan", out var planObj) && planObj is Plan plan)
                        {
                            await _logger.LogPlanAsync(plan, context, ct).ConfigureAwait(false);
                            return ToolResult.Ok();
                        }
                        return ToolResult.NotFound("CurrentPlan in context");

                    case "run":
                        if (context.Metadata.TryGetValue("CurrentRun", out var runObj) && runObj is ExecutionUnit run)
                        {
                            await _logger.LogRunAsync(run, context, ct).ConfigureAwait(false);
                            return ToolResult.Ok();
                        }
                        return ToolResult.NotFound("CurrentRun in context");

                    case "step":
                        if (!string.IsNullOrEmpty(targetId) &&
                            context.Metadata.TryGetValue($"Step_{targetId}", out var stepObj) &&
                            stepObj is TaskNode step)
                        {
                            await _logger.LogStepAsync(step, context, ct).ConfigureAwait(false);
                            return ToolResult.Ok();
                        }
                        return ToolResult.NotFound($"Step {targetId}");

                    default:
                        return ToolResult.BadRequest($"Invalid log type: {logType}");
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Error(ex);
            }
        }
    }
}