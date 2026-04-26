namespace Valaiorp.Configuration.Models
{
    public sealed class LlmConfig
    {
        /// <summary>Provider identifier: "anthropic" | "openai" | "ollama" | "custom".</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>Model ID as the provider names it (e.g. "claude-sonnet-4-6", "gpt-4o").</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// API key. Leave empty to resolve from the environment variable named in ApiKeyEnvVar.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Environment variable that holds the API key (default: "{PROVIDER}_API_KEY").
        /// Used when ApiKey is null or empty.
        /// </summary>
        public string? ApiKeyEnvVar { get; set; }

        /// <summary>
        /// Path to a local file whose first non-empty line is the API key.
        /// Useful for Docker secrets mounts or CI artifact drops.
        /// Used when ApiKey and ApiKeyEnvVar are both unset.
        /// </summary>
        public string? ApiKeyFile { get; set; }

        /// <summary>Base URL override (useful for Ollama or proxied endpoints).</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Default system prompt template. Supports {variables} substitution.</summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        public int MaxTokens { get; set; } = 4096;
        public float Temperature { get; set; } = 0.7f;

        /// <summary>HTTP request timeout for LLM calls.</summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Resolves the API key synchronously: literal → env var → file.
        /// For async vault providers use IApiKeyProvider / AddLlmClient(config, provider).
        /// </summary>
        public string? ResolveApiKey()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
                return ApiKey;

            var envVar = ApiKeyEnvVar ?? $"{Provider.ToUpperInvariant()}_API_KEY";
            var fromEnv = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            if (!string.IsNullOrWhiteSpace(ApiKeyFile) && File.Exists(ApiKeyFile))
            {
                var fromFile = File.ReadLines(ApiKeyFile)
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                    ?.Trim();
                if (!string.IsNullOrWhiteSpace(fromFile))
                    return fromFile;
            }

            return null;
        }
    }
}
