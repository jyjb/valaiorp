namespace Valaiorp.Core.Contracts
{
    /// <summary>
    /// Resolves API keys from any credential store — env vars, files, vaults, config, etc.
    /// Implement this interface to plug in Azure Key Vault, AWS Secrets Manager,
    /// HashiCorp Vault, or any other credential backend.
    /// </summary>
    public interface IApiKeyProvider
    {
        /// <summary>
        /// Returns the secret value for the given logical key name, or null if not found.
        /// </summary>
        /// <param name="keyName">
        /// The logical name identifying this secret (e.g. "ANTHROPIC_API_KEY").
        /// How this maps to a vault path or secret ID is up to the implementation.
        /// </param>
        Task<string?> GetApiKeyAsync(string keyName, CancellationToken ct = default);
    }
}
