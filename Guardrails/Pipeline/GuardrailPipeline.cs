namespace Valaiorp.Guardrails.Pipeline
{
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
    ///
    /// Thread-safety: Add/Remove are lock-guarded to preserve insertion order.
    /// Evaluation takes a snapshot of the list so long-running async checks never
    /// block registrations on other threads.
    /// </summary>
    public sealed class GuardrailPipeline : IGuardrailPipeline
    {
        private readonly List<IGuardrail> _guardrails = new();
        private readonly Lock _lock = new();

        public void Add(IGuardrail guardrail)
        {
            lock (_lock)
            {
                if (_guardrails.All(g => g.Id != guardrail.Id))
                    _guardrails.Add(guardrail);
            }
        }

        public void Remove(string guardrailId)
        {
            lock (_lock)
                _guardrails.RemoveAll(g => g.Id == guardrailId);
        }

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
            // Snapshot once so Add/Remove during evaluation don't affect this run.
            IGuardrail[] snapshot;
            lock (_lock)
                snapshot = [.. _guardrails];

            var current = content;
            var warnings = new List<string>();

            foreach (var guardrail in snapshot)
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

            if (warnings.Count > 0)
                return GuardrailResult.Warn(
                    guardrailId: "pipeline",
                    reason: string.Join("; ", warnings),
                    metadata: new Dictionary<string, object> { ["SafeContent"] = current ?? string.Empty });

            return GuardrailResult.Allow(current);
        }
    }
}
