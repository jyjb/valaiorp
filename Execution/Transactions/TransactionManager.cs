namespace Valaiorp.Execution.Transactions
{
    using System.Collections.Concurrent;
    using Valaiorp.Execution.Models;

    public sealed class TransactionManager
    {
        private readonly ConcurrentStack<ExecutionUnit> _transactionStack = new();

        public void BeginTransaction(ExecutionUnit unit)
        {
            _transactionStack.Push(unit);
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_transactionStack.TryPop(out var unit))
            {
                unit.Status = ExecutionStatus.RolledBack;
                foreach (var node in unit.Graph.Nodes.Values)
                {
                    if (node.Status == TaskStatus.Completed)
                    {
                        // Logic to undo the effects of the executed step
                        node.Status = TaskStatus.RolledBack;
                    }
                }
            }
        }

        public void Commit()
        {
            if (_transactionStack.TryPop(out var unit))
            {
                unit.Status = ExecutionStatus.Completed;
            }
        }
    }
}