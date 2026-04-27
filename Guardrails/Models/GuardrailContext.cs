namespace Valaiorp.Guardrails.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Guardrails.Enums;

    public sealed class GuardrailContext
    {
        public required IExecutionContext ExecutionContext { get; init; }
        public required GuardrailScope Scope { get; init; }

        /// <summary>The text to evaluate (user prompt for Input; LLM response for Output).</summary>
        public string? Content { get; init; }

        /// <summary>Tool identifier — set for Tool-scope evaluations.</summary>
        public string? ToolId { get; init; }

        /// <summary>Tool input parameters — set for Tool-scope evaluations.</summary>
        public IReadOnlyDictionary<string, object>? ToolInputs { get; init; }

        /// <summary>Tool output values — set when evaluating post-tool results.</summary>
        public IReadOnlyDictionary<string, object>? ToolOutputs { get; init; }
    }
}
