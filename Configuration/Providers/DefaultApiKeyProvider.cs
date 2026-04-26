namespace Valaiorp.Configuration.Providers
{
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Resolution order: ApiKey (literal) → ApiKeyEnvVar (environment variable)
    /// → ApiKeyFile (path to a file whose first line is the key).
    /// Falls back to the conventional "{PROVIDER}_API_KEY" env var if no env var name is set.
    /// </summary>
    public sealed class DefaultApiKeyProvider : IApiKeyProvider
    {
        private readonly LlmConfig _config;

        public DefaultApiKeyProvider(LlmConfig config) => _config = config;

        public Task<string?> GetApiKeyAsync(string keyName, CancellationToken ct = default)
        {
            // 1. Literal value embedded in config (dev/local only — avoid in prod)
            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
                return Task.FromResult<string?>(_config.ApiKey);

            // 2. Environment variable
            var envVar = _config.ApiKeyEnvVar ?? keyName;
            var fromEnv = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return Task.FromResult<string?>(fromEnv);

            // 3. File — first non-empty line (e.g. a secrets file on a secured mount)
            if (!string.IsNullOrWhiteSpace(_config.ApiKeyFile) && File.Exists(_config.ApiKeyFile))
            {
                var fromFile = File.ReadLines(_config.ApiKeyFile)
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                    ?.Trim();
                if (!string.IsNullOrWhiteSpace(fromFile))
                    return Task.FromResult<string?>(fromFile);
            }

            return Task.FromResult<string?>(null);
        }
    }
}
