namespace Valaiorp.Execution.Executors
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Retry.Contracts;
    using Valaiorp.Tools.Resolvers;

    public sealed class ParallelExecutor
    {
        private readonly ToolResolver _toolResolver;
        private readonly IRetryStrategy _retryStrategy;
        private readonly int _maxDegreeOfParallelism;

        public ParallelExecutor(ToolResolver toolResolver, IRetryStrategy retryStrategy, int maxDegreeOfParallelism = 4)
        {
            _toolResolver = toolResolver;
            _retryStrategy = retryStrategy;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        public async Task ExecuteAsync(
            ExecutionUnit unit,
            CancellationToken ct = default)
        {
            unit.Status = ExecutionStatus.Running;
            var executionTasks = new ConcurrentDictionary<string, Task<IExecutionResult>>();
            var executionOrder = unit.Graph.GetExecutionOrder();

            var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);

            foreach (var nodeId in executionOrder)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                var node = unit.Graph.Nodes[nodeId];
                node.Status = TaskStatus.Running;

                executionTasks.TryAdd(nodeId, Task.Run(async () =>
                {
                    try
                    {
                        if (node.Step.ToolId != null)
                        {
                            var result = await _retryStrategy.ExecuteWithRetryAsync(
                                (ctx, token) => _toolResolver.ExecuteToolAsync(
                                    node.Step.ToolId,
                                    ctx,
                                    node.Step.Inputs?.ToString() ?? string.Empty,
                                    token),
                                unit.Context,
                                ct)
                            .ConfigureAwait(false);

                            node.Result = result;
                            node.Status = result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed;
                            return result;
                        }
                        return new ExecutionResult(node.Id, unit.Context.Id, true);
                    }
                    catch (Exception ex)
                    {
                        node.Exception = ex;
                        node.Status = TaskStatus.Failed;
                        return new ExecutionResult(node.Id, unit.Context.Id, false, ex.Message, ex);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(executionTasks.Values).ConfigureAwait(false);

            unit.Status = executionTasks.Values.All(t => t.Result.IsSuccess)
                ? ExecutionStatus.Completed
                : ExecutionStatus.Failed;
            unit.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

}
