namespace Valaiorp.Execution.Models
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Planner.Models;

    public sealed class TaskGraph
    {
        private readonly ConcurrentDictionary<string, TaskNode> _nodes = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _dependencies = new();

        public IReadOnlyDictionary<string, TaskNode> Nodes => _nodes;
        public IReadOnlyDictionary<string, HashSet<string>> Dependencies => _dependencies;

        public void AddNode(PlanStep step)
        {
            var node = new TaskNode(step);
            _nodes.TryAdd(node.Id, node);
            _dependencies.TryAdd(node.Id, new HashSet<string>());
        }

        public void AddDependency(string nodeId, string dependsOnId)
        {
            if (_nodes.ContainsKey(nodeId) && _nodes.ContainsKey(dependsOnId))
            {
                _dependencies[nodeId].Add(dependsOnId);
            }
        }

        public IReadOnlyList<string> GetExecutionOrder()
        {
            var order = new List<string>();
            var visited = new HashSet<string>();
            var temp = new HashSet<string>();

            foreach (var node in _nodes.Keys)
            {
                Visit(node, visited, temp, order);
            }

            order.Reverse();
            return order;
        }

        private void Visit(
            string nodeId,
            HashSet<string> visited,
            HashSet<string> temp,
            List<string> order)
        {
            if (temp.Contains(nodeId))
            {
                throw new InvalidOperationException("Cycle detected in task graph.");
            }

            if (visited.Contains(nodeId))
            {
                return;
            }

            temp.Add(nodeId);

            if (_dependencies.TryGetValue(nodeId, out var dependencies))
            {
                foreach (var dep in dependencies)
                {
                    Visit(dep, visited, temp, order);
                }
            }

            temp.Remove(nodeId);
            visited.Add(nodeId);
            order.Add(nodeId);
        }
    }

    public sealed class TaskNode
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public PlanStep Step { get; }
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public Exception? Exception { get; set; }
        public IExecutionResult? Result { get; set; }
        public bool AiUsed { get; set; }
        public TokenUsage? LlmTokens { get; set; }

        public TaskNode(PlanStep step)
        {
            Step = step;
            Id = step.Id;
        }
    }

    public enum TaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        RolledBack
    }
}