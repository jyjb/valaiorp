namespace Valaiorp.Execution.Transactions
{
    using Valaiorp.Execution.Models;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Resolvers;

    /// <summary>
    /// Stateless transaction coordinator. Each call to RollbackAsync / Commit operates
    /// on the ExecutionUnit supplied by the caller, so concurrent BotWorker executions
    /// never interfere with each other.
    ///
    /// Real rollback: tools that implement <see cref="ICompensatable"/> have their
    /// CompensateAsync called in reverse execution order. Tools that don't implement
    /// the interface are marked RolledBack in memory only.
    /// </summary>
    public sealed class TransactionManager
    {
        private readonly ToolResolver? _toolResolver;

        public TransactionManager(ToolResolver? toolResolver = null)
        {
            _toolResolver = toolResolver;
        }

        public void BeginTransaction(ExecutionUnit unit)
        {
            unit.Status = ExecutionStatus.Running;
        }

        public async Task RollbackAsync(ExecutionUnit unit, CancellationToken ct = default)
        {
            unit.Status = ExecutionStatus.RolledBack;

            var completedNodes = unit.Graph.Nodes.Values
                .Where(n => n.Status == TaskStatus.Completed && n.Result != null)
                .OrderByDescending(n => n.ExecutedAt ?? DateTimeOffset.MinValue)
                .ToList();

            foreach (var node in completedNodes)
            {
                node.Status = TaskStatus.RolledBack;

                if (_toolResolver == null || node.Step.ToolId == null)
                    continue;

                var tool = _toolResolver.ResolveTool(node.Step.ToolId);
                if (tool is ICompensatable compensatable)
                {
                    try
                    {
                        await compensatable.CompensateAsync(
                            unit.Context,
                            (IReadOnlyDictionary<string, object>)node.Result!.Outputs,
                            ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        node.Exception = new AggregateException(
                            $"Compensation failed for step '{node.Step.Name}'.", ex);
                    }
                }
            }
        }

        public void Commit(ExecutionUnit unit)
        {
            unit.Status = ExecutionStatus.Completed;
        }
    }
}
