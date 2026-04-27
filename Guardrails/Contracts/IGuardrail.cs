namespace Valaiorp.Guardrails.Contracts
{
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    public interface IGuardrail
    {
        string Id { get; }
        string Name { get; }

        /// <summary>Which pipeline stage(s) this guardrail runs in.</summary>
        GuardrailScope Scope { get; }

        bool IsEnabled { get; set; }

        Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default);
    }
}
