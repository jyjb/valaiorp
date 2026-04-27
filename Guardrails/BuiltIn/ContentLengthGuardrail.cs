namespace Valaiorp.Guardrails.BuiltIn
{
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Blocks content that exceeds a configured character limit.
    /// Apply separate instances for input and output limits.
    /// </summary>
    public sealed class ContentLengthGuardrail : IGuardrail
    {
        private readonly int _maxChars;

        public string Id   { get; }
        public string Name { get; }
        public GuardrailScope Scope { get; }
        public bool IsEnabled { get; set; } = true;

        public ContentLengthGuardrail(GuardrailScope scope, int maxChars)
        {
            Scope     = scope;
            _maxChars = maxChars;
            Id        = $"content-length-{scope.ToString().ToLowerInvariant()}-guardrail";
            Name      = $"Content Length ({scope})";
        }

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var content = context.Content;
            if (string.IsNullOrEmpty(content) || content.Length <= _maxChars)
                return Task.FromResult(GuardrailResult.Allow(content));

            return Task.FromResult(GuardrailResult.Block(
                guardrailId: Id,
                reason: $"Content length {content.Length} chars exceeds the {_maxChars}-char limit"));
        }
    }
}
