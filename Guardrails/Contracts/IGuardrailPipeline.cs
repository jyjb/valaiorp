namespace Valaiorp.Guardrails.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Guardrails.Models;

    public interface IGuardrailPipeline
    {
        void Add(IGuardrail guardrail);
        void Remove(string guardrailId);

        /// <summary>
        /// Evaluate the user prompt / plan input before it reaches the planner or LLM.
        /// Returns a redacted SafeContent when PII or banned content is found and the
        /// configured action is Redact rather than Block.
        /// </summary>
        Task<GuardrailResult> EvaluateInputAsync(
            IExecutionContext context,
            string content,
            CancellationToken ct = default);

        /// <summary>
        /// Evaluate an LLM response or execution output after it is produced but before
        /// it is committed or returned to the caller.
        /// </summary>
        Task<GuardrailResult> EvaluateOutputAsync(
            IExecutionContext context,
            string content,
            CancellationToken ct = default);

        /// <summary>
        /// Evaluate a tool call before it executes. toolInputs are the resolved parameters;
        /// toolOutputs is null at call time and populated for post-call checks.
        /// </summary>
        Task<GuardrailResult> EvaluateToolCallAsync(
            IExecutionContext context,
            string toolId,
            IReadOnlyDictionary<string, object> inputs,
            CancellationToken ct = default);
    }
}
