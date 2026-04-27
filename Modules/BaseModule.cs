namespace Valaiorp.Modules
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Tools.Contracts;

    public abstract class BaseModule : IModule
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract IReadOnlyDictionary<string, ParameterDefinition> Parameters { get; }
        public abstract IReadOnlyCollection<ITool> Tools { get; }

        public virtual Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task<ModuleResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            var stepResults = new List<ToolResult>();
            try
            {
                foreach (var tool in Tools)
                {
                    ct.ThrowIfCancellationRequested();
                    var toolParams = BuildToolParameters(tool, parameters);
                    var result = await tool.ExecuteAsync(context, toolParams, ct).ConfigureAwait(false);
                    stepResults.Add(result);
                    if (!result.IsSuccess)
                        return ModuleResult.Error($"Step '{tool.Name}' failed.", stepResults);
                }
                return ModuleResult.Ok(stepResults.LastOrDefault()?.Results, stepResults);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ModuleResult.Error(ex, stepResults);
            }
        }

        // Override to build per-tool parameters from the module-level parameters dict.
        // Default: pass the full parameters dict to every tool.
        protected virtual IReadOnlyDictionary<string, object> BuildToolParameters(
            ITool tool,
            IReadOnlyDictionary<string, object> parameters) => parameters;
    }
}
