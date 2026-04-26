namespace Valaiorp.Tools.Registries
{
    using System.Collections.Concurrent;
    using Valaiorp.Tools.Contracts;

    public sealed class ToolRegistry
    {
        private readonly ConcurrentDictionary<string, ITool> _tools = new();

        public IReadOnlyDictionary<string, ITool> Tools => _tools;

        public void Register(ITool tool)
        {
            _tools.TryAdd(tool.Id, tool);
        }

        public bool TryGetTool(string toolId, out ITool? tool)
        {
            return _tools.TryGetValue(toolId, out tool);
        }

        public void Unregister(string toolId)
        {
            _tools.TryRemove(toolId, out _);
        }

        public void Clear()
        {
            _tools.Clear();
        }
    }
}