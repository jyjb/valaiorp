namespace Valaiorp.Guardrails.BuiltIn
{
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Models;

    /// <summary>
    /// Enforces a whitelist and/or blacklist of permitted tool IDs.
    /// When AllowedToolIds is provided, only those tool IDs may execute.
    /// When DeniedToolIds is provided, those tool IDs are always blocked.
    /// Denied list takes precedence over allowed list.
    /// </summary>
    public sealed class ToolScopeGuardrail : IGuardrail
    {
        private readonly HashSet<string>? _allowed;
        private readonly HashSet<string>? _denied;

        public string Id   { get; } = "tool-scope-guardrail";
        public string Name { get; } = "Tool Scope";
        public GuardrailScope Scope { get; } = GuardrailScope.Tool;
        public bool IsEnabled { get; set; } = true;

        public ToolScopeGuardrail(
            IEnumerable<string>? allowedToolIds = null,
            IEnumerable<string>? deniedToolIds  = null)
        {
            if (allowedToolIds?.Any() == true)
                _allowed = new HashSet<string>(allowedToolIds, StringComparer.OrdinalIgnoreCase);

            if (deniedToolIds?.Any() == true)
                _denied = new HashSet<string>(deniedToolIds, StringComparer.OrdinalIgnoreCase);
        }

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default)
        {
            var toolId = context.ToolId;
            if (string.IsNullOrEmpty(toolId))
                return Task.FromResult(GuardrailResult.Allow());

            if (_denied?.Contains(toolId) == true)
                return Task.FromResult(GuardrailResult.Block(
                    guardrailId: Id,
                    reason: $"Tool \"{toolId}\" is explicitly denied"));

            if (_allowed is not null && !_allowed.Contains(toolId))
                return Task.FromResult(GuardrailResult.Block(
                    guardrailId: Id,
                    reason: $"Tool \"{toolId}\" is not in the allowed tool list"));

            return Task.FromResult(GuardrailResult.Allow());
        }
    }
}
