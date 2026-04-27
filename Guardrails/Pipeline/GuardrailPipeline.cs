namespace Valaiorp.Guardrails.Pipeline
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Runs all registered guardrails in insertion order for each scope.
    ///
    /// Evaluation rules:
    ///   Block or Escalate — first violation wins; pipeline stops immediately.
    ///   Redact            — redacted content is forwarded to the next guardrail.
    ///   Warn              — logged in the result metadata; pipeline continues.
    /// </summary>
    public sealed class GuardrailPipeline : IGuardrailPipeline
    {
        private readonly ConcurrentDictionary<string, IGuardrail> _guardrails = new();

        public void Add(IGuardrail guardrail)    => _guardrails.TryAdd(guardrail.Id, guardrail);
        public void Remove(string guardrailId)   => _guardrails.TryRemove(guardrailId, out _);

        public Task<GuardrailResult> EvaluateInputAsync(
            IExecutionContext context, string content, CancellationToken ct = default)
            => RunAsync(GuardrailScope.Input, context, content, null, null, ct);

        public Task<GuardrailResult> EvaluateOutputAsync(
            IExecutionContext context, string content, CancellationToken ct = default)
            => RunAsync(GuardrailScope.Output, context, content, null, null, ct);

        public Task<GuardrailResult> EvaluateToolCallAsync(
            IExecutionContext context,
            string toolId,
            IReadOnlyDictionary<string, object> inputs,
            CancellationToken ct = default)
            => RunAsync(GuardrailScope.Tool, context, null, toolId, inputs, ct);

        // ── Core pipeline ─────────────────────────────────────────────────────────

        private async Task<GuardrailResult> RunAsync(
            GuardrailScope scope,
            IExecutionContext executionContext,
            string? content,
            string? toolId,
            IReadOnlyDictionary<string, object>? toolInputs,
            CancellationToken ct)
        {
            var current = content;
            var warnings = new List<string>();

            foreach (var guardrail in _guardrails.Values)
            {
                if (!guardrail.IsEnabled) continue;
                if (guardrail.Scope != GuardrailScope.All && guardrail.Scope != scope) continue;

                var ctx = new GuardrailContext
                {
                    ExecutionContext = executionContext,
                    Scope           = scope,
                    Content         = current,
                    ToolId          = toolId,
                    ToolInputs      = toolInputs
                };

                var result = await guardrail.EvaluateAsync(ctx, ct).ConfigureAwait(false);

                switch (result.Action)
                {
                    case ViolationAction.Block:
                    case ViolationAction.Escalate:
                        return result;

                    case ViolationAction.Redact when result.SafeContent is not null:
                        current = result.SafeContent;
                        break;

                    case ViolationAction.Warn:
                        if (result.Reason is not null) warnings.Add($"[{guardrail.Id}] {result.Reason}");
                        break;
                }
            }

            var finalResult = GuardrailResult.Allow(current);

            if (warnings.Count > 0)
                return GuardrailResult.Warn(
                    guardrailId: "pipeline",
                    reason: string.Join("; ", warnings),
                    metadata: new Dictionary<string, object> { ["SafeContent"] = current ?? string.Empty });

            return finalResult;
        }
    }
}
