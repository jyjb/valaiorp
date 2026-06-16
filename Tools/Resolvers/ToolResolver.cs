namespace Valaiorp.Tools.Resolvers
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Governance;
    using Valaiorp.Tools.Registries;
    using ExecutionContext = Valaiorp.Core.Contracts.ExecutionContext;

    public sealed class ToolResolver
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly ModuleRegistry _moduleRegistry;
        private readonly IExecutionGate _gate;
        private readonly Func<IModule, ITool> _moduleToolFactory;

        public ToolResolver(
            ToolRegistry toolRegistry,
            ModuleRegistry moduleRegistry,
            IExecutionGate gate,
            Func<IModule, ITool>? moduleToolFactory = null)
        {
            _toolRegistry = toolRegistry;
            _moduleRegistry = moduleRegistry;
            _gate = gate ?? throw new ArgumentNullException(nameof(gate),
                "An IExecutionGate is required so every tool call is governed. " +
                "The runtime registers UnwiredExecutionGate by default; call " +
                "services.AddGovernance(...) to install a real policy.");
            _moduleToolFactory = moduleToolFactory ?? (m => m.Tools.First());
        }

        public ITool? ResolveTool(string toolId)
        {
            // 1. Direct tool match
            if (_toolRegistry.TryGetTool(toolId, out var tool))
                return tool;

            // 2. Module callable as a unit
            if (_moduleRegistry.TryGetModule(toolId, out var module) && module is not null)
                return _moduleToolFactory(module);

            // 3. Individual tool nested inside a module
            foreach (var m in _moduleRegistry.Modules.Values)
            {
                var nested = m.Tools.FirstOrDefault(t => t.Id == toolId);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        /// <summary>
        /// Executes a tool from inside an agent's RunAsync without requiring an IExecutionContext.
        /// Builds a minimal context from the AgentMessage so agent authors don't have to.
        /// </summary>
        public Task<IExecutionResult> ExecuteToolAsync(
            string toolId,
            AgentMessage message,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            var context = new Core.Contracts.ExecutionContext
            {
                SessionId = message.ConversationId,
                Metadata  = new Dictionary<string, object>(message.Payload),
                Prompt    = message.Prompt
            };
            return ExecuteToolAsync(toolId, context, parameters, ct);
        }

        public async Task<IExecutionResult> ExecuteToolAsync(
            string toolId,
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            var tool = ResolveTool(toolId)
                ?? throw new InvalidOperationException($"Tool '{toolId}' not found.");

            // Mandatory governance gate: every tool call must be authorized before it runs.
            var decision = await _gate.AuthorizeAsync(toolId, context, parameters, ct).ConfigureAwait(false);
            if (!decision.IsAllowed)
                return new ExecutionResult(toolId, context.Id, false, decision.Reason);

            var toolResult = await tool.ExecuteAsync(context, parameters, ct).ConfigureAwait(false);
            var annotated  = toolResult with { StepId = toolId };

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
                : annotated.Errors != null ? System.Text.Json.JsonSerializer.Serialize(annotated.Errors) : null;

            return new ExecutionResult(toolId, context.Id, annotated.IsSuccess, errorMessage, null, outputs);
        }
    }
}
