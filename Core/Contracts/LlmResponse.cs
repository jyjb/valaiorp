namespace Valaiorp.Core.Contracts
{
    public sealed class LlmResponse
    {
        public bool IsSuccess { get; init; }
        public string Content { get; init; } = string.Empty;
        public string? FinishReason { get; init; }
        public int? InputTokens { get; init; }
        public int? OutputTokens { get; init; }
        public string? Error { get; init; }
        public string? ModelId { get; init; }

        /// <summary>
        /// Populated when the LLM responded with a native tool/function call instead of text.
        /// Check this before using <see cref="Content"/> — if ToolCalls is non-empty,
        /// Content will typically be empty or a brief thought string.
        /// </summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

        public bool HasToolCalls => ToolCalls.Count > 0;

        public static LlmResponse Success(string content, string? finishReason = null,
            int? inputTokens = null, int? outputTokens = null, string? modelId = null,
            IReadOnlyList<ToolCall>? toolCalls = null)
            => new()
            {
                IsSuccess    = true,
                Content      = content,
                FinishReason = finishReason,
                InputTokens  = inputTokens,
                OutputTokens = outputTokens,
                ModelId      = modelId,
                ToolCalls    = toolCalls ?? []
            };

        public static LlmResponse Failure(string error)
            => new() { IsSuccess = false, Error = error };
    }

    /// <summary>
    /// A single tool/function call requested by the LLM in its response.
    /// Populated by <see cref="LlmResponse.ToolCalls"/> when the model uses
    /// native function-calling (Anthropic tool_use, OpenAI tool_calls).
    /// </summary>
    public sealed class ToolCall
    {
        /// <summary>Provider-assigned call ID (e.g. Anthropic toolu_xxx, OpenAI call_xxx).</summary>
        public string CallId { get; init; } = string.Empty;

        /// <summary>Tool/function name as declared in the system prompt or tool spec.</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Arguments the LLM wants to pass to the tool.</summary>
        public IReadOnlyDictionary<string, object> Inputs { get; init; }
            = new Dictionary<string, object>();
    }
}
