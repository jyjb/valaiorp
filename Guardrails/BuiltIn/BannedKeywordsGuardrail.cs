namespace Valaiorp.Guardrails.BuiltIn
{
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Blocks any content that contains a banned keyword (case-insensitive).
    /// Configure the keyword list via GuardrailConfig.BannedKeywords or pass directly.
    /// </summary>
    public sealed class BannedKeywordsGuardrail : IGuardrail
    {
        private readonly HashSet<string> _keywords;

        public string Id   { get; } = "banned-keywords-guardrail";
        public string Name { get; } = "Banned Keywords";
        public GuardrailScope Scope { get; } = GuardrailScope.All;
        public bool IsEnabled { get; set; } = true;

        public BannedKeywordsGuardrail(IEnumerable<string> keywords)
        {
            _keywords = new HashSet<string>(
                keywords.Select(k => k.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var content = context.Content;
            if (string.IsNullOrEmpty(content) || _keywords.Count == 0)
                return Task.FromResult(GuardrailResult.Allow(content));

            foreach (var keyword in _keywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(GuardrailResult.Block(
                        guardrailId: Id,
                        reason: $"Content contains banned keyword: \"{keyword}\""));
            }

            return Task.FromResult(GuardrailResult.Allow(content));
        }
    }
}
