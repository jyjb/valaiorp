namespace Valaiorp.Modules
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Registries;

    public sealed class ModuleExecutor
    {
        private readonly ModuleRegistry _registry;

        public ModuleExecutor(ModuleRegistry registry) => _registry = registry;

        public async Task<ModuleResult> ExecuteAsync(
            string moduleId,
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            if (!_registry.TryGetModule(moduleId, out var module) || module is null)
                return ModuleResult.Error($"Module '{moduleId}' not found.");

            await module.InitializeAsync(ct).ConfigureAwait(false);
            return await module.ExecuteAsync(context, parameters, ct).ConfigureAwait(false);
        }

        public IReadOnlyDictionary<string, IModule> GetAll() => _registry.Modules;
    }
}
