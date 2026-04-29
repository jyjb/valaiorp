namespace Valaiorp.Execution.Executors
{
    using System.Text.Json;
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;
    using Valaiorp.Execution.Models;
    using Valaiorp.Planner.Models;
    using Valaiorp.Retry.Contracts;
    using Valaiorp.Tools.Resolvers;

    public sealed class ParallelExecutor
    {
        private readonly ToolResolver _toolResolver;
        private readonly IRetryStrategy _retryStrategy;
        private readonly int _maxDegreeOfParallelism;
        private readonly AutonomyConfig _autonomy;
        private readonly IEscalationService? _escalation;
        private readonly IAgentRegistry? _agentRegistry;

        public ParallelExecutor(
            ToolResolver toolResolver,
            IRetryStrategy retryStrategy,
            int maxDegreeOfParallelism = 4,
            AutonomyConfig? autonomy = null,
            IEscalationService? escalation = null,
            IAgentRegistry? agentRegistry = null)
        {
            _toolResolver = toolResolver;
            _retryStrategy = retryStrategy;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _autonomy = autonomy ?? new AutonomyConfig();
            _escalation = escalation;
            _agentRegistry = agentRegistry;
        }

        public async Task ExecuteAsync(ExecutionUnit unit, CancellationToken ct = default)
        {
            unit.Status = ExecutionStatus.Running;
            var loopCounters  = new Dictionary<string, int>();
            const int maxLoopIterations = 1000;
            var totalLoopIterations = 0;

            bool continueLoop;
            do
            {
            continueLoop = false;

            var alreadyDone  = unit.Graph.Nodes.Values
                .Where(n => n.Status == TaskStatus.Completed)
                .Select(n => n.Id)
                .ToHashSet();

            var pendingOrder = unit.Graph.GetExecutionOrder()
                .Where(id => unit.Graph.Nodes[id].Status == TaskStatus.Pending)
                .ToList();

            var waves    = BuildWaves(pendingOrder, unit.Graph.Dependencies, alreadyDone);
            var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);

            foreach (var wave in waves)
            {
                if (ct.IsCancellationRequested) break;

                var waveTasks = wave.Select(nodeId =>
                {
                    var node = unit.Graph.Nodes[nodeId];
                    node.Status = TaskStatus.Running;

                    return Task.Run(async () =>
                    {
                        node.ExecutedAt = DateTimeOffset.UtcNow;
                        var acquired = false;
                        try
                        {
                            await semaphore.WaitAsync(ct).ConfigureAwait(false);
                            acquired = true;

                            // ── Condition guard ───────────────────────────────────────
                            if (!string.IsNullOrEmpty(node.Step.Condition) &&
                                !EvaluateCondition(node.Step.Condition, unit.Plan.WorkflowState))
                            {
                                var skipped = new ExecutionResult(node.Id, unit.Context.Id, true,
                                    outputs: new Dictionary<string, object>
                                    {
                                        ["_skipped"] = true,
                                        ["_reason"]  = $"Condition false: {node.Step.Condition}"
                                    });
                                node.Result = skipped;
                                node.Status = TaskStatus.Completed;
                                return skipped;
                            }

                            if (node.Step.AgentId != null)
                            {
                                if (_agentRegistry == null)
                                {
                                    var noReg = new ExecutionResult(node.Id, unit.Context.Id, false,
                                        $"Step '{node.Step.Name}' targets agent '{node.Step.AgentId}' " +
                                        "but no IAgentRegistry is registered. " +
                                        "Call services.AddMultiAgentRuntime() to enable agent delegation.");
                                    node.Result = noReg;
                                    node.Status = TaskStatus.Failed;
                                    return noReg;
                                }

                                var agent = _agentRegistry.Resolve(node.Step.AgentId);
                                if (agent == null)
                                {
                                    var notFound = new ExecutionResult(node.Id, unit.Context.Id, false,
                                        $"Agent '{node.Step.AgentId}' not found in registry.");
                                    node.Result = notFound;
                                    node.Status = TaskStatus.Failed;
                                    return notFound;
                                }

                                // HITL for high-risk agent delegation
                                if (node.Step.IsHighRisk && _autonomy.RequireApprovalForHighRisk)
                                {
                                    if (_escalation == null)
                                        throw new InvalidOperationException(
                                            $"Step '{node.Step.Name}' is marked high-risk and " +
                                            "RequireApprovalForHighRisk is true, but no IEscalationService is registered.");

                                    var approval = await _escalation.RequestApprovalAsync(
                                        unit.Context,
                                        action: node.Step.AgentId,
                                        description: node.Step.Description,
                                        ct: ct).ConfigureAwait(false);

                                    if (!approval.IsApproved)
                                    {
                                        var denied = new ExecutionResult(
                                            node.Id, unit.Context.Id, false,
                                            $"Agent step '{node.Step.Name}' rejected by approver " +
                                            $"'{approval.ApproverId}': {approval.Reason}");
                                        node.Result = denied;
                                        node.Status = TaskStatus.Failed;
                                        return denied;
                                    }
                                }

                                var payload = node.Step.Inputs != null
                                    ? new Dictionary<string, object>(node.Step.Inputs)
                                    : new Dictionary<string, object>();

                                var message = new AgentMessage
                                {
                                    ConversationId = unit.Context.SessionId,
                                    FromAgentId    = null,
                                    ToAgentId      = node.Step.AgentId,
                                    Payload        = payload,
                                    Prompt         = unit.Context.Prompt ?? new PromptContext
                                    {
                                        UserPrompt = node.Step.Description
                                    }
                                };

                                var agentResult = await agent.RunAsync(message, ct).ConfigureAwait(false);

                                var outputs = new Dictionary<string, object>(agentResult.Metadata);
                                if (agentResult.Output != null)
                                    outputs["output"] = agentResult.Output;

                                var execResult = agentResult.IsSuccess
                                    ? new ExecutionResult(node.Id, unit.Context.Id, true, outputs: outputs)
                                    : new ExecutionResult(node.Id, unit.Context.Id, false,
                                        agentResult.Error ?? "Agent execution failed");

                                node.Result = execResult;
                                node.Status = agentResult.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed;
                                return execResult;
                            }

                            if (node.Step.ToolId != null)
                            {
                                // HITL: request approval before executing any high-risk step.
                                if (node.Step.IsHighRisk && _autonomy.RequireApprovalForHighRisk)
                                {
                                    if (_escalation == null)
                                        throw new InvalidOperationException(
                                            $"Step '{node.Step.Name}' is marked high-risk and " +
                                            "RequireApprovalForHighRisk is true, but no IEscalationService " +
                                            "is registered. Call services.AddEscalationInterfaces() and " +
                                            "provide IApprovalProvider, IOverrideProvider, and IEscalationHandler.");

                                    var approval = await _escalation.RequestApprovalAsync(
                                        unit.Context,
                                        action: node.Step.ToolId,
                                        description: node.Step.Description,
                                        ct: ct).ConfigureAwait(false);

                                    if (!approval.IsApproved)
                                    {
                                        var denied = new ExecutionResult(
                                            node.Id, unit.Context.Id, false,
                                            $"Step '{node.Step.Name}' rejected by approver " +
                                            $"'{approval.ApproverId}': {approval.Reason}");
                                        node.Result = denied;
                                        node.Status = TaskStatus.Failed;
                                        return denied;
                                    }
                                }

                                // Stamp the circuit key so the circuit breaker tracks per tool.
                                var contextWithKey = StampCircuitKey(unit.Context, node.Step.ToolId);
                                var parameters = ResolveParameters(node.Step.Inputs, unit.Graph.Nodes);

                                var result = await _retryStrategy.ExecuteWithRetryAsync(
                                    (ctx, token) => _toolResolver.ExecuteToolAsync(node.Step.ToolId, ctx, parameters, token),
                                    contextWithKey, ct).ConfigureAwait(false);

                                node.Result = result;
                                node.Status = result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed;
                                if (!result.IsSuccess && node.Exception == null)
                                    node.Exception = new InvalidOperationException(
                                        result.ErrorMessage ?? $"Step '{node.Step.Name}' failed (tool returned IsSuccess=false)");
                                if (result.IsSuccess)
                                    foreach (var kv in result.Outputs)
                                        unit.Plan.WorkflowState[kv.Key] = kv.Value;
                                ApplyLlmTokensFromOutputs(node, result.Outputs);
                                return result;
                            }

                            var noToolResult = new ExecutionResult(node.Id, unit.Context.Id, true);
                            node.Result = noToolResult;
                            node.Status = TaskStatus.Completed;
                            return noToolResult;
                        }
                        catch (OperationCanceledException ex)
                        {
                            // Propagate cancellation — mark the node so the failure is recorded,
                            // then rethrow so Task.WhenAll observes the cancellation.
                            node.Exception = ex;
                            node.Status = TaskStatus.Failed;
                            throw;
                        }
                        catch (Exception ex)
                        {
                            node.Exception = ex;
                            node.Status = TaskStatus.Failed;
                            return new ExecutionResult(node.Id, unit.Context.Id, false, ex.Message, ex);
                        }
                        finally
                        {
                            if (acquired) semaphore.Release();
                        }
                    }, ct);
                }).ToList();

                await Task.WhenAll(waveTasks).ConfigureAwait(false);
            }

            // ── Loop continuation ─────────────────────────────────────────────────
            foreach (var loopEndNode in unit.Graph.Nodes.Values
                         .Where(n => n.Status == TaskStatus.Completed && n.Step.IsLoopEnd
                                  && !string.IsNullOrEmpty(n.Step.LoopStartId)))
            {
                loopCounters.TryGetValue(loopEndNode.Step.LoopStartId!, out var count);
                var nextCount = count + 1;
                if (!string.IsNullOrEmpty(loopEndNode.Step.LoopCondition) &&
                    EvaluateLoopCondition(loopEndNode.Step.LoopCondition, unit.Plan.WorkflowState, nextCount))
                {
                    loopCounters[loopEndNode.Step.LoopStartId!] = nextCount;
                    unit.Plan.WorkflowState["iteration"] = nextCount;
                    ResetLoopBody(unit, loopEndNode.Step.LoopStartId!);
                    continueLoop = true;
                    totalLoopIterations++;
                    break;
                }
            }

            } while (continueLoop && totalLoopIterations < maxLoopIterations);

            // Aggregate per-node failures onto unit.Exception so LogRunAsync and callers
            // have a consolidated view of what failed, not just status = Failed with no detail.
            var failedNodes = unit.Graph.Nodes.Values
                .Where(n => n.Status == TaskStatus.Failed && n.Exception != null)
                .ToList();

            if (failedNodes.Count > 0 && unit.Exception == null)
                unit.Exception = failedNodes.Count == 1
                    ? failedNodes[0].Exception
                    : new AggregateException(
                        $"{failedNodes.Count} step(s) failed",
                        failedNodes.Select(n => n.Exception!));

            unit.Status = unit.Graph.Nodes.Values.All(n => n.Status == TaskStatus.Completed)
                ? ExecutionStatus.Completed
                : ExecutionStatus.Failed;
            unit.CompletedAt = DateTimeOffset.UtcNow;
        }

        // ── Wave building ─────────────────────────────────────────────────────────

        private static List<List<string>> BuildWaves(
            IReadOnlyList<string> topologicalOrder,
            IReadOnlyDictionary<string, HashSet<string>> dependencies,
            HashSet<string>? preDone = null)
        {
            var waves     = new List<List<string>>();
            var done      = preDone != null ? new HashSet<string>(preDone) : new HashSet<string>();
            var remaining = new List<string>(topologicalOrder);

            while (remaining.Count > 0)
            {
                var wave = remaining
                    .Where(id => !dependencies.TryGetValue(id, out var deps) || deps.All(done.Contains))
                    .ToList();

                if (wave.Count == 0)
                    wave = remaining.ToList(); // safety: run everything left to avoid deadlock

                foreach (var id in wave) { remaining.Remove(id); done.Add(id); }
                waves.Add(wave);
            }
            return waves;
        }

        // ── Circuit key ───────────────────────────────────────────────────────────

        // Returns a shallow wrapper with the circuit key stamped into metadata.
        // This lets the circuit breaker key per-tool without changing IExecutionContext.
        private static IExecutionContext StampCircuitKey(IExecutionContext context, string toolId)
        {
            if (context.Metadata.ContainsKey("_circuitKey"))
                return context;

            var meta = new Dictionary<string, object>(context.Metadata) { ["_circuitKey"] = toolId };
            return new Core.Contracts.ExecutionContext
            {
                Id                = context.Id,
                SessionId         = context.SessionId,
                UserId            = context.UserId,
                ExpiresAt         = context.ExpiresAt,
                Metadata          = meta,
                Steps             = context.Steps,
                CancellationToken = context.CancellationToken,
                Prompt            = context.Prompt
            };
        }

        // ── Parameter resolution ──────────────────────────────────────────────────

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

        private static object? Resolve(string path, Dictionary<string, TaskNode> nameToNode)
        {
            var dot      = path.IndexOf('.');
            var stepName = dot < 0 ? path : path[..dot];
            var rest     = dot < 0 ? null : path[(dot + 1)..];

            if (!nameToNode.TryGetValue(stepName, out var node) || node.Result == null)
                return null;

            return rest == null ? node.Result.Outputs : WalkOutputs(node.Result.Outputs, rest);
        }

        private static object? WalkOutputs(IDictionary<string, object> outputs, string path)
        {
            var dot  = path.IndexOf('.');
            var key  = dot < 0 ? path : path[..dot];
            var rest = dot < 0 ? null : path[(dot + 1)..];

            if (!outputs.TryGetValue(key, out var value)) return null;
            if (rest == null) return value;

            if (value is not IDictionary<string, object> dict)
            {
                var json = JsonSerializer.Serialize(value);
                var elem = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (elem == null) return null;
                dict = elem;
            }

            return WalkOutputs(dict, rest);
        }

        // ── LLM token lifting ─────────────────────────────────────────────────────

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
            int i => i,
            long l => (int)l,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } je => je.GetInt32(),
            _ => 0
        };

        // ── Condition / loop helpers ──────────────────────────────────────────────

        private static bool EvaluateCondition(string condition, IDictionary<string, object?> state)
        {
            try
            {
                if (!condition.StartsWith("WorkflowState['"))
                    return false;

                var keyEnd = condition.IndexOf("']", StringComparison.Ordinal);
                if (keyEnd < 0) return false;

                var key = condition[15..keyEnd];
                if (!state.TryGetValue(key, out var rawValue)) return false;

                var opPart = condition[(keyEnd + 2)..].TrimStart();
                var strValue = rawValue?.ToString() ?? string.Empty;

                if (opPart.StartsWith("CONTAINS ", StringComparison.OrdinalIgnoreCase))
                    return strValue.Contains(opPart[9..].Trim(), StringComparison.OrdinalIgnoreCase);
                if (opPart.StartsWith("=="))
                    return strValue == opPart[2..].Trim();
                if (opPart.StartsWith("!="))
                    return strValue != opPart[2..].Trim();
                if (opPart.StartsWith(">") && rawValue is int iv  && int.TryParse(opPart[1..].Trim(), out var rv))
                    return iv > rv;
                if (opPart.StartsWith("<") && rawValue is int iv2 && int.TryParse(opPart[1..].Trim(), out var rv2))
                    return iv2 < rv2;

                return false;
            }
            catch { return false; }
        }

        private static bool EvaluateLoopCondition(string condition, IDictionary<string, object?> state, int iteration)
        {
            try
            {
                if (condition.StartsWith("iteration", StringComparison.OrdinalIgnoreCase))
                {
                    var op = condition.Contains("<=") ? "<=" :
                             condition.Contains(">=") ? ">=" :
                             condition.Contains("==") ? "==" :
                             condition.Contains('<')  ? "<"  :
                             condition.Contains('>')  ? ">"  : null;

                    if (op != null)
                    {
                        var limitStr = condition[(condition.IndexOf(op, StringComparison.Ordinal) + op.Length)..].Trim();
                        if (int.TryParse(limitStr, out var limit))
                            return op switch
                            {
                                "<"  => iteration < limit,
                                "<=" => iteration <= limit,
                                ">"  => iteration > limit,
                                ">=" => iteration >= limit,
                                "==" => iteration == limit,
                                _    => false
                            };
                    }
                }
                else if (condition.StartsWith("WorkflowState['"))
                {
                    return EvaluateCondition(condition, state);
                }

                return true;
            }
            catch { return false; }
        }

        private static void ResetLoopBody(ExecutionUnit unit, string loopStartId)
        {
            var steps = unit.Plan.Steps;
            var loopStartIdx = -1;
            var loopEndIdx   = -1;

            // loopStartId is the *name* of the loop-start step (as set by the LLM planner).
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Name.Equals(loopStartId, StringComparison.OrdinalIgnoreCase))
                    loopStartIdx = i;
                if (steps[i].IsLoopEnd
                    && steps[i].LoopStartId != null
                    && steps[i].LoopStartId!.Equals(loopStartId, StringComparison.OrdinalIgnoreCase))
                    loopEndIdx = i;
            }

            if (loopStartIdx < 0 || loopEndIdx < 0) return;

            // Reset everything from the step after LoopStart through LoopEnd so they
            // re-execute on the next outer-loop pass. LoopStart itself is not reset —
            // it's a setup/marker step that should only run once.
            for (var i = loopStartIdx + 1; i <= loopEndIdx; i++)
            {
                if (unit.Graph.Nodes.TryGetValue(steps[i].Id, out var node))
                {
                    node.Status    = TaskStatus.Pending;
                    node.Result    = null;
                    node.Exception = null;
                    node.ExecutedAt = default;
                    node.AiUsed    = false;
                    node.LlmTokens = null;
                }
            }
        }
    }
}
