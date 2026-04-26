namespace Valaiorp.Core.Contracts
{
    /// <summary>
    /// Provider-agnostic LLM interface. Implement this in a separate provider project
    /// (e.g. Valaiorp.LlmProviders) using HttpClient + System.Text.Json — no SDK deps required.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>Unique identifier for this client / provider (e.g. "anthropic-claude-sonnet-4-6").</summary>
        string ClientId { get; }

        /// <summary>Sends a prompt and returns the full completion.</summary>
        Task<LlmResponse> CompleteAsync(PromptContext prompt, CancellationToken ct = default);
    }
}
