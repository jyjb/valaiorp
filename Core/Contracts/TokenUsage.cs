namespace Valaiorp.Core.Contracts
{
    public sealed record TokenUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public int TotalTokens => InputTokens + OutputTokens;
        public string? ModelId { get; init; }

        public static TokenUsage From(LlmResponse response)
            => new()
            {
                InputTokens  = response.InputTokens  ?? 0,
                OutputTokens = response.OutputTokens ?? 0,
                ModelId      = response.ModelId
            };

        public static TokenUsage operator +(TokenUsage a, TokenUsage b)
            => new()
            {
                InputTokens  = a.InputTokens  + b.InputTokens,
                OutputTokens = a.OutputTokens + b.OutputTokens,
                ModelId      = b.ModelId ?? a.ModelId
            };
    }
}
