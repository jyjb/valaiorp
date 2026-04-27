namespace Valaiorp.Execution.Workflow
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Executors;
    using Valaiorp.Execution.Models;
    using Valaiorp.Tools.Resolvers;

    public sealed class WorkflowExecutor
    {
        private readonly ToolResolver _toolResolver;
        private readonly ParallelExecutor _parallelExecutor;
        private readonly ConcurrentDictionary<string, int> _loopCounters = new();

        public WorkflowExecutor(ToolResolver toolResolver, ParallelExecutor parallelExecutor)
        {
            _toolResolver = toolResolver;
            _parallelExecutor = parallelExecutor;
        }

        public async Task<IExecutionResult> ExecuteAsync(
            IReadOnlyList<WorkflowStep> steps,
            WorkflowExecutionContext context,
            CancellationToken ct = default)
        {
            if (steps.Count == 0)
            {
                return new ExecutionResult(context.Id, context.Id, false, "No steps provided in workflow.");
            }

            var currentStep = steps[0];
            var visitedSteps = new HashSet<string>();
            var executionResults = new List<IExecutionResult>();
            const int maxIterations = 1000;
            var iterations = 0;

            while (currentStep != null && iterations < maxIterations)
            {
                iterations++;
                if (visitedSteps.Contains(currentStep.Id) && !currentStep.IsLoopStart)
                {
                    return new ExecutionResult(context.Id, context.Id, false,
                        $"Potential infinite loop detected at step {currentStep.Id}.");
                }

                if (currentStep.IsLoopStart)
                {
                    _loopCounters.TryAdd(currentStep.Id, 0);
                }

                if (currentStep.Tool != ToolType.None
                    && !string.IsNullOrEmpty(currentStep.ToolId)
                    && !string.IsNullOrEmpty(currentStep.Input))
                {
                    var result = await _toolResolver.ExecuteToolAsync(
                        currentStep.ToolId,
                        context,
                        new Dictionary<string, object> { ["input"] = currentStep.Input },
                        ct)
                    .ConfigureAwait(false);

                    executionResults.Add(result);
                    if (!result.IsSuccess)
                    {
                        return new ExecutionResult(context.Id, context.Id, false,
                            $"Step '{currentStep.Name}' failed: {result.ErrorMessage}", result.Exception);
                    }

                    foreach (var output in result.Outputs)
                    {
                        context.WorkflowState[output.Key] = output.Value;
                    }
                }

                if (currentStep.IsLoopEnd && _loopCounters.TryGetValue(currentStep.Id, out var count))
                {
                    _loopCounters[currentStep.Id] = count + 1;
                    if (!EvaluateLoopCondition(currentStep.LoopCondition!, context, count + 1))
                    {
                        _loopCounters.TryRemove(currentStep.Id, out _);
                        visitedSteps.Remove(currentStep.Id);
                    }
                }

                if (!currentStep.IsLoopStart && !currentStep.IsLoopEnd)
                {
                    visitedSteps.Add(currentStep.Id);
                }

                currentStep = GetNextStep(steps, currentStep, context)!;
            }

            if (iterations >= maxIterations)
            {
                return new ExecutionResult(context.Id, context.Id, false,
                    "Maximum iterations reached. Possible infinite loop.");
            }

            return new ExecutionResult(context.Id, context.Id, true, null, null,
                new Dictionary<string, object>
                {
                    { "ExecutedSteps", visitedSteps.Count },
                    { "Results", executionResults }
                });
        }

        private WorkflowStep? GetNextStep(
            IReadOnlyList<WorkflowStep> steps,
            WorkflowStep currentStep,
            WorkflowExecutionContext context)
        {
            if (string.IsNullOrEmpty(currentStep.NextStepId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(currentStep.Condition))
            {
                if (EvaluateCondition(currentStep.Condition, context))
                {
                    return steps.FirstOrDefault(s => s.Id == currentStep.NextStepId);
                }

                var elseStepId = currentStep.NextStepId.StartsWith('!')
                    ? currentStep.NextStepId[1..]
                    : null;
                return steps.FirstOrDefault(s => s.Id == elseStepId);
            }

            return steps.FirstOrDefault(s => s.Id == currentStep.NextStepId);
        }

        // Evaluates simple conditions of the form: WorkflowState['Key'] > 5
        private bool EvaluateCondition(string condition, WorkflowExecutionContext context)
        {
            try
            {
                if (!condition.StartsWith("WorkflowState['"))
                {
                    return false;
                }

                var keyEnd = condition.IndexOf("']", StringComparison.Ordinal);
                if (keyEnd < 0) return false;

                // "WorkflowState['" is 15 chars, key starts at index 15
                var key = condition[15..keyEnd];
                if (!context.WorkflowState.TryGetValue(key, out var value)) return false;

                var opPart = condition[(keyEnd + 2)..].TrimStart();
                if (opPart.StartsWith("=="))
                    return value?.ToString() == opPart[2..].Trim();
                if (opPart.StartsWith("!="))
                    return value?.ToString() != opPart[2..].Trim();
                if (opPart.StartsWith(">") && value is int iv && int.TryParse(opPart[1..].Trim(), out var rv))
                    return iv > rv;
                if (opPart.StartsWith("<") && value is int iv2 && int.TryParse(opPart[1..].Trim(), out var rv2))
                    return iv2 < rv2;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool EvaluateLoopCondition(string condition, WorkflowExecutionContext context, int iteration)
        {
            try
            {
                if (condition.StartsWith("iteration", StringComparison.OrdinalIgnoreCase))
                {
                    var op = condition.Contains("==") ? "==" :
                             condition.Contains("<=") ? "<=" :
                             condition.Contains(">=") ? ">=" :
                             condition.Contains('<') ? "<" :
                             condition.Contains('>') ? ">" : null;

                    if (op != null)
                    {
                        var limitStr = condition[(condition.IndexOf(op, StringComparison.Ordinal) + op.Length)..].Trim();
                        if (int.TryParse(limitStr, out var limit))
                        {
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
                }
                else if (condition.StartsWith("WorkflowState['"))
                {
                    return EvaluateCondition(condition, context);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
