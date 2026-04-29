namespace Valaiorp.Tools.Registries
{
    using System.Collections.Concurrent;
    using System.Text;
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

        /// <summary>
        /// Returns a formatted list of all registered tools suitable for inclusion in an
        /// LLM system prompt. Agent authors call this to tell the LLM which tools are available
        /// without re-implementing tool discovery themselves.
        ///
        /// Format per tool:
        ///   - {id}: {description}
        /// </summary>
        public string FormatForSystemPrompt()
        {
            if (_tools.IsEmpty) return string.Empty;
            var sb = new StringBuilder();
            foreach (var tool in _tools.Values)
                sb.AppendLine($"- {tool.Id}: {tool.Description}");
            return sb.ToString().TrimEnd();
        }
    }
}