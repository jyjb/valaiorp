namespace Valaiorp.Guardrails.BuiltIn
{
    using System.Text.RegularExpressions;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Classifies content by sensitivity level and attaches the classification
    /// as metadata. Never blocks — always allows but emits a warning when the
    /// classification exceeds Internal, enabling downstream logging and audit.
    /// </summary>
    public sealed class DataClassificationGuardrail : IGuardrail
    {
        public string Id   { get; } = "data-classification-guardrail";
        public string Name { get; } = "Data Classification";
        public GuardrailScope Scope { get; } = GuardrailScope.All;
        public bool IsEnabled { get; set; } = true;

        // Ordered highest-to-lowest so the first match wins
        private static readonly (Regex Pattern, DataClassification Level)[] _rules =
        [
            (new Regex(@"\b(password|secret|api[_\-]?key|private[_\-]?key|token|credential|bearer)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled), DataClassification.Restricted),

            (new Regex(@"\b(ssn|social[_\-\s]security|credit[_\-\s]card|passport|dob|date[_\-\s]of[_\-\s]birth)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled), DataClassification.Restricted),

            (new Regex(@"\b(salary|payroll|medical|diagnosis|patient|health[_\-\s]record|hipaa)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled), DataClassification.Confidential),

            (new Regex(@"\b(internal|proprietary|confidential|nda|trade[_\-\s]secret)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled), DataClassification.Internal),
        ];

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var content = context.Content;
            if (string.IsNullOrEmpty(content))
                return Task.FromResult(GuardrailResult.Allow(content));

            var classification = DataClassification.Public;

            foreach (var (pattern, level) in _rules)
            {
                if (pattern.IsMatch(content))
                {
                    classification = level;
                    break;
                }
            }

            if (classification == DataClassification.Public)
                return Task.FromResult(GuardrailResult.Allow(content));

            var metadata = new Dictionary<string, object>
            {
                ["DataClassification"] = classification.ToString(),
                ["ClassifiedAt"]       = DateTimeOffset.UtcNow
            };

            // Restricted content generates a warning for audit; the caller may choose to escalate.
            return Task.FromResult(GuardrailResult.Warn(
                guardrailId: Id,
                reason: $"Content classified as {classification}",
                metadata: metadata));
        }
    }
}
