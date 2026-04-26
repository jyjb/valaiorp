namespace Valaiorp.Tools.Resolvers
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Registries;

    public sealed class ToolResolver
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly ModuleRegistry _moduleRegistry;

        public ToolResolver(ToolRegistry toolRegistry, ModuleRegistry moduleRegistry)
        {
            _toolRegistry = toolRegistry;
            _moduleRegistry = moduleRegistry;
        }

        public ITool? ResolveTool(string toolId)
        {
            if (_toolRegistry.TryGetTool(toolId, out var tool))
                return tool;

            foreach (var module in _moduleRegistry.Modules.Values)
            {
                var moduleTool = module.Tools.FirstOrDefault(t => t.Id == toolId);
                if (moduleTool != null)
                    return moduleTool;
            }

            return null;
        }

        public async Task<IExecutionResult> ExecuteToolAsync(
            string toolId,
            IExecutionContext context,
            string input,
            CancellationToken ct = default)
        {
            var tool = ResolveTool(toolId)
                ?? throw new InvalidOperationException($"Tool with ID '{toolId}' not found.");

            var toolResult = await tool.ExecuteAsync(context, input, ct).ConfigureAwait(false);
            var annotated = toolResult with { StepId = toolId };

            var outputs = new Dictionary<string, object>
            {
                ["Status"]    = annotated.Status,
                ["IsSuccess"] = annotated.IsSuccess,
                ["StepId"]    = toolId,
            };
            if (annotated.Results != null) outputs["Results"] = annotated.Results;
            if (annotated.Errors  != null) outputs["Errors"]  = annotated.Errors;

            var errorMessage = annotated.IsSuccess ? null
                : annotated.Errors is string s ? s
                : annotated.Errors != null ? JsonSerializer.Serialize(annotated.Errors) : null;

            return new ExecutionResult(toolId, context.Id, annotated.IsSuccess, errorMessage, null, outputs);
        }
    }
}
