namespace Valaiorp.LlmProviders.Profiles
{
    using System.Reflection;
    using System.Text.Json;

    public static class LlmProviderProfileLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Loads one of the built-in provider profiles (anthropic, openai, ollama).
        /// To add a new provider without writing C#, use <see cref="LoadFromFile"/> instead.
        /// </summary>
        public static LlmProviderProfile LoadBuiltIn(string provider)
        {
            var name = provider.ToLowerInvariant();
            var resourceName = $"Valaiorp.LlmProviders.Profiles.{name}.json";
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"No built-in profile for provider '{provider}'. " +
                    "Built-in providers: anthropic, openai, ollama, gemini, mistral, cohere. " +
                    "For other providers call LlmProviderProfileLoader.LoadFromFile(path).");

            using (stream)
                return JsonSerializer.Deserialize<LlmProviderProfile>(stream, Options)!;
        }

        /// <summary>
        /// Loads a provider profile from a JSON file on disk.
        /// Use this to add new providers (Gemini, Mistral, Cohere, etc.) without any C# code.
        /// </summary>
        public static LlmProviderProfile LoadFromFile(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<LlmProviderProfile>(stream, Options)!;
        }

        /// <summary>Loads a provider profile from a JSON string.</summary>
        public static LlmProviderProfile LoadFromJson(string json)
            => JsonSerializer.Deserialize<LlmProviderProfile>(json, Options)!;
    }
}
