namespace Valaiorp.LlmProviders.Profiles
{
    using System.Text.Json;

    public sealed class LlmProviderProfile
    {
        /// <summary>Base URL used when the caller does not supply one.</summary>
        public string DefaultBaseUrl { get; set; } = "";

        /// <summary>Path portion of the completions endpoint, e.g. "/v1/messages".</summary>
        public string Endpoint { get; set; } = "";

        /// <summary>
        /// Header used to pass the API key.
        /// Value supports the {apiKey} placeholder, e.g. "Bearer {apiKey}".
        /// Null for providers that need no key (Ollama local).
        /// </summary>
        public AuthHeaderSpec? AuthHeader { get; set; }

        /// <summary>Additional static headers required by the provider, e.g. anthropic-version.</summary>
        public List<FixedHeaderSpec> FixedHeaders { get; set; } = new();

        /// <summary>
        /// "topLevel"    — system prompt is sent as a separate top-level field (Anthropic).
        /// "firstMessage" — system prompt is the first message with role "system" (OpenAI, Ollama).
        /// </summary>
        public string SystemPosition { get; set; } = "firstMessage";

        public RequestBodySpec RequestBody { get; set; } = new();
        public ResponseMappingSpec ResponseMapping { get; set; } = new();
    }

    public sealed class AuthHeaderSpec
    {
        public string Name { get; set; } = "";
        /// <summary>{apiKey} is replaced at runtime.</summary>
        public string Value { get; set; } = "";
    }

    public sealed class FixedHeaderSpec
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public sealed class RequestBodySpec
    {
        public string ModelKey { get; set; } = "model";

        /// <summary>Null = omit max_tokens from the request (e.g. Ollama).</summary>
        public string? MaxTokensKey { get; set; } = "max_tokens";

        /// <summary>
        /// Dot-path to the temperature field.
        /// "temperature" = top level; "options.temperature" = nested under options.
        /// </summary>
        public string TemperaturePath { get; set; } = "temperature";

        /// <summary>Extra static fields merged into the request body, e.g. {"stream": false}.</summary>
        public Dictionary<string, JsonElement> FixedFields { get; set; } = new();
    }

    public sealed class ResponseMappingSpec
    {
        /// <summary>Dot-path to the assistant text, e.g. "content.0.text" or "choices.0.message.content".</summary>
        public string ContentPath { get; set; } = "";
        public string? FinishReasonPath { get; set; }
        public string? InputTokensPath { get; set; }
        public string? OutputTokensPath { get; set; }
        public string? ModelPath { get; set; }
    }
}
