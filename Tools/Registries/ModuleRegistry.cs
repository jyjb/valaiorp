namespace Valaiorp.Tools.Registries
{
    using System.Collections.Concurrent;
    using Valaiorp.Tools.Contracts;

    public sealed class ModuleRegistry
    {
        private readonly ConcurrentDictionary<string, IModule> _modules = new();

        public IReadOnlyDictionary<string, IModule> Modules => _modules;

        public void Register(IModule module)
        {
            _modules.TryAdd(module.Id, module);
        }

        public bool TryGetModule(string moduleId, out IModule? module)
        {
            return _modules.TryGetValue(moduleId, out module);
        }

        public void Unregister(string moduleId)
        {
            _modules.TryRemove(moduleId, out _);
        }

        public void Clear()
        {
            _modules.Clear();
        }
    }
}