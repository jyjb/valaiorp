namespace Valaiorp.Modules
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;

    // Adapts IModule to ITool so modules flow through the existing execution pipeline
    // without any changes to ToolResolver or ParallelExecutor.
    public sealed class ModuleTool : ITool
    {
        private readonly IModule _module;

        public ModuleTool(IModule module) => _module = module;

        public string Id => _module.Id;
        public string Name => _module.Name;
        public string Description => _module.Description;
        public ToolType Type => ToolType.Module;
        public IReadOnlyDictionary<string, object> Metadata =>
            _module.Parameters.ToDictionary(p => p.Key, p => (object)p.Value);

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            var result = await _module.ExecuteAsync(context, parameters, ct).ConfigureAwait(false);
            return result.IsSuccess
                ? ToolResult.Ok(result.Results)
                : ToolResult.Error(result.Errors?.ToString() ?? "Module execution failed.");
        }
    }
}
