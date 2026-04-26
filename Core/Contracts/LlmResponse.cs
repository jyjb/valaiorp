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

        public static LlmResponse Success(string content, string? finishReason = null,
            int? inputTokens = null, int? outputTokens = null, string? modelId = null)
            => new() { IsSuccess = true, Content = content, FinishReason = finishReason,
                       InputTokens = inputTokens, OutputTokens = outputTokens, ModelId = modelId };

        public static LlmResponse Failure(string error)
            => new() { IsSuccess = false, Error = error };
    }
}
