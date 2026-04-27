namespace Valaiorp.Guardrails.BuiltIn
{
    using System.Text.RegularExpressions;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Detects and redacts common PII patterns (email, phone, SSN, credit card, IP address).
    /// Default action is Redact — sensitive values are replaced with typed placeholders
    /// so execution can continue with sanitised content.
    /// </summary>
    public sealed class PiiGuardrail : IGuardrail
    {
        public string Id   { get; } = "pii-guardrail";
        public string Name { get; } = "PII Redaction";
        public GuardrailScope Scope { get; } = GuardrailScope.All;
        public bool IsEnabled { get; set; } = true;

        private static readonly (Regex Pattern, string Placeholder)[] _patterns =
        [
            (new Regex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled), "[EMAIL]"),

            (new Regex(@"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]\d{3}[-.\s]\d{4}\b",
                RegexOptions.Compiled), "[PHONE]"),

            (new Regex(@"\b\d{3}-\d{2}-\d{4}\b",
                RegexOptions.Compiled), "[SSN]"),

            // 13–16 digit card numbers with optional spaces/dashes
            (new Regex(@"\b(?:\d[ \-]?){13,16}\b",
                RegexOptions.Compiled), "[CARD]"),

            (new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b",
                RegexOptions.Compiled), "[IP]"),
        ];

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var content = context.Content;
            if (string.IsNullOrEmpty(content))
                return Task.FromResult(GuardrailResult.Allow(content));

            var redacted = content;
            var found = new List<string>();

            foreach (var (pattern, placeholder) in _patterns)
            {
                var replaced = pattern.Replace(redacted, placeholder);
                if (replaced != redacted)
                {
                    found.Add(placeholder);
                    redacted = replaced;
                }
            }

            if (found.Count == 0)
                return Task.FromResult(GuardrailResult.Allow(content));

            return Task.FromResult(GuardrailResult.Redact(
                guardrailId: Id,
                safeContent: redacted,
                reason: $"PII detected and redacted: {string.Join(", ", found)}"));
        }
    }
}
