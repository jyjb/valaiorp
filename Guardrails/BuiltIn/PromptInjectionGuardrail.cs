namespace Valaiorp.Guardrails.BuiltIn
{
    using System.Text.RegularExpressions;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Detects prompt injection attempts — phrases designed to override system instructions
    /// or assume an unauthorized identity/role.
    /// Scope: Input only (attacks originate in user-controlled content).
    /// </summary>
    public sealed class PromptInjectionGuardrail : IGuardrail
    {
        public string Id   { get; } = "prompt-injection-guardrail";
        public string Name { get; } = "Prompt Injection Detection";
        public GuardrailScope Scope { get; } = GuardrailScope.Input;
        public bool IsEnabled { get; set; } = true;

        private static readonly Regex[] _patterns =
        [
            new(@"ignore\s+(all\s+|previous\s+|your\s+|prior\s+)?(instructions?|rules?|guidelines?|constraints?|directives?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"disregard\s+(all\s+|your\s+|previous\s+)?(instructions?|rules?|guidelines?|constraints?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"forget\s+(everything|all\s+previous|your\s+instructions?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"you\s+are\s+now\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"your\s+new\s+(role|persona|identity|task|purpose|name|directive)\s+is",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"pretend\s+(you\s+are|to\s+be)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"\bact\s+as\s+(a\s+|an\s+|the\s+)?(?!assistant|helpful)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"\broleplay\s+as\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"\bdo\s+anything\s+now\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"\bjailbreak\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            new(@"\b(DAN|DUDE|STAN|KEVIN)\b",
                RegexOptions.Compiled),

            new(@"system\s+prompt",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ];

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var content = context.Content;
            if (string.IsNullOrEmpty(content))
                return Task.FromResult(GuardrailResult.Allow(content));

            foreach (var pattern in _patterns)
            {
                if (pattern.IsMatch(content))
                    return Task.FromResult(GuardrailResult.Block(
                        guardrailId: Id,
                        reason: "Prompt injection attempt detected"));
            }

            return Task.FromResult(GuardrailResult.Allow(content));
        }
    }
}
