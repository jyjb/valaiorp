namespace Valaiorp.LlmProviders.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Configuration.Models;
    using Valaiorp.Configuration.Providers;
    using Valaiorp.Core.Contracts;
    using Valaiorp.LlmProviders.Clients;
    using Valaiorp.LlmProviders.Profiles;

    public static class LlmProviderServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an <see cref="ILlmClient"/> from a built-in provider profile.
        /// Supported providers: "anthropic", "openai", "ollama", "gemini", "mistral", "cohere".
        /// API key resolution order: ApiKey (literal) → ApiKeyEnvVar → ApiKeyFile → "{PROVIDER}_API_KEY" env var.
        /// </summary>
        public static IServiceCollection AddLlmClient(
            this IServiceCollection services,
            LlmConfig config)
            => services.AddLlmClient(config, new DefaultApiKeyProvider(config));

        /// <summary>
        /// Registers an <see cref="ILlmClient"/> using a custom <see cref="IApiKeyProvider"/>.
        /// Plug in Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, or any credential backend.
        /// </summary>
        public static IServiceCollection AddLlmClient(
            this IServiceCollection services,
            LlmConfig config,
            IApiKeyProvider keyProvider)
        {
            var apiKey = keyProvider
                .GetApiKeyAsync($"{config.Provider.ToUpperInvariant()}_API_KEY")
                .GetAwaiter().GetResult();

            var profile = LlmProviderProfileLoader.LoadBuiltIn(config.Provider);
            ILlmClient client = new GenericLlmClient(
                profile,
                config.ModelId,
                config.MaxTokens,
                config.Temperature,
                apiKey,
                config.BaseUrl);

            return services.AddSingleton<ILlmClient>(client);
        }

        /// <summary>
        /// Registers an <see cref="ILlmClient"/> from a custom provider profile JSON file.
        /// Use this to add providers not built in (Gemini, Mistral, Cohere, etc.)
        /// without writing any C# code — just supply the profile JSON.
        /// </summary>
        public static IServiceCollection AddLlmClientFromProfile(
            this IServiceCollection services,
            string profileFilePath,
            LlmConfig config,
            IApiKeyProvider? keyProvider = null)
        {
            keyProvider ??= new DefaultApiKeyProvider(config);
            var apiKey = keyProvider
                .GetApiKeyAsync($"{config.Provider.ToUpperInvariant()}_API_KEY")
                .GetAwaiter().GetResult();

            var profile = LlmProviderProfileLoader.LoadFromFile(profileFilePath);
            ILlmClient client = new GenericLlmClient(
                profile,
                config.ModelId,
                config.MaxTokens,
                config.Temperature,
                apiKey,
                config.BaseUrl);

            return services.AddSingleton<ILlmClient>(client);
        }
    }
}
