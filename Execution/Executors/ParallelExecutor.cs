namespace Valaiorp.Execution.Executors
{
    using System.Collections.Concurrent;
    using System.Text.Json;
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

        public async Task ExecuteAsync(ExecutionUnit unit, CancellationToken ct = default)
        {
            unit.Status = ExecutionStatus.Running;
            var executionTasks = new ConcurrentDictionary<string, Task<IExecutionResult>>();
            var executionOrder = unit.Graph.GetExecutionOrder();
            var semaphore      = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);

            foreach (var nodeId in executionOrder)
            {
                if (ct.IsCancellationRequested) break;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                var node = unit.Graph.Nodes[nodeId];
                node.Status = TaskStatus.Running;

                executionTasks.TryAdd(nodeId, Task.Run(async () =>
                {
                    try
                    {
                        if (node.Step.ToolId != null)
                        {
                            var parameters = ResolveParameters(node.Step.Inputs, unit.Graph.Nodes);

                            var result = await _retryStrategy.ExecuteWithRetryAsync(
                                (ctx, token) => _toolResolver.ExecuteToolAsync(node.Step.ToolId, ctx, parameters, token),
                                unit.Context,
                                ct).ConfigureAwait(false);

                            node.Result = result;
                            node.Status = result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed;
                            ApplyLlmTokensFromOutputs(node, result.Outputs);
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

        // Resolves ${StepName.Results.Field} references from completed step outputs.
        private static IReadOnlyDictionary<string, object> ResolveParameters(
            IReadOnlyDictionary<string, object>? inputs,
            IReadOnlyDictionary<string, TaskNode> nodes)
        {
            if (inputs == null || inputs.Count == 0)
                return new Dictionary<string, object>();

            var nameToNode = nodes.Values
                .Where(n => n.Result != null)
                .GroupBy(n => n.Step.Name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var resolved = new Dictionary<string, object>(inputs.Count);
            foreach (var (key, value) in inputs)
            {
                if (value is string s && s.StartsWith("${") && s.EndsWith("}"))
                    resolved[key] = Resolve(s[2..^1], nameToNode) ?? value;
                else
                    resolved[key] = value;
            }
            return resolved;
        }

        // Resolves a dotted path like "ReadFile.Results.Content" against completed step outputs.
        private static object? Resolve(string path, Dictionary<string, TaskNode> nameToNode)
        {
            var dot = path.IndexOf('.');
            var stepName = dot < 0 ? path : path[..dot];
            var rest     = dot < 0 ? null : path[(dot + 1)..];

            if (!nameToNode.TryGetValue(stepName, out var node) || node.Result == null)
                return null;

            if (rest == null)
                return node.Result.Outputs;

            return WalkOutputs(node.Result.Outputs, rest);
        }

        private static object? WalkOutputs(IDictionary<string, object> outputs, string path)
        {
            var dot  = path.IndexOf('.');
            var key  = dot < 0 ? path : path[..dot];
            var rest = dot < 0 ? null : path[(dot + 1)..];

            if (!outputs.TryGetValue(key, out var value))
                return null;

            if (rest == null)
                return value;

            // Descend into anonymous objects via JSON round-trip
            if (value is not IDictionary<string, object> dict)
            {
                var json = JsonSerializer.Serialize(value);
                var elem = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (elem == null) return null;
                dict = elem;
            }

            return WalkOutputs(dict, rest);
        }

        // Convention: tools that call an LLM internally include _llmInputTokens / _llmOutputTokens
        // in their output dict. This method lifts those values onto the TaskNode for logging.
        private static void ApplyLlmTokensFromOutputs(TaskNode node, IDictionary<string, object> outputs)
        {
            var hasInput  = outputs.TryGetValue("_llmInputTokens",  out var rawIn);
            var hasOutput = outputs.TryGetValue("_llmOutputTokens", out var rawOut);
            if (!hasInput && !hasOutput) return;

            outputs.TryGetValue("_llmModelId", out var rawModel);

            node.AiUsed = true;
            node.LlmTokens = new TokenUsage
            {
                InputTokens  = ToInt(rawIn),
                OutputTokens = ToInt(rawOut),
                ModelId      = rawModel as string
            };
        }

        private static int ToInt(object? value) => value switch
        {
            int i                                                     => i,
            long l                                                    => (int)l,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } je => je.GetInt32(),
            _                                                         => 0
        };
    }
}
