namespace Valaiorp.Knowledge.Resolvers
{
    using System.Collections.Concurrent;
    using System.Reflection;
    using Valaiorp.Knowledge.Contracts;

    public sealed class KnowledgeProviderResolver
    {
        private readonly ConcurrentDictionary<string, IKnowledgeProvider> _providers = new();
        private string _defaultProviderId = string.Empty;

        public IReadOnlyDictionary<string, IKnowledgeProvider> Providers => _providers;

        public void RegisterProvider(IKnowledgeProvider provider, bool setAsDefault = false)
        {
            _providers.TryAdd(provider.Id, provider);
            if (setAsDefault || string.IsNullOrEmpty(_defaultProviderId))
            {
                _defaultProviderId = provider.Id;
            }
        }

        public bool TryGetProvider(string providerId, out IKnowledgeProvider? provider)
        {
            return _providers.TryGetValue(providerId, out provider);
        }

        public IKnowledgeProvider? GetDefaultProvider()
        {
            if (string.IsNullOrEmpty(_defaultProviderId))
            {
                return _providers.Values.FirstOrDefault();
            }
            return _providers.TryGetValue(_defaultProviderId, out var provider) ? provider : null;
        }

        public void SetDefaultProvider(string providerId)
        {
            if (_providers.ContainsKey(providerId))
            {
                _defaultProviderId = providerId;
            }
        }

        public void UnregisterProvider(string providerId)
        {
            _providers.TryRemove(providerId, out _);
            if (_defaultProviderId == providerId)
            {
                _defaultProviderId = _providers.Keys.FirstOrDefault() ?? string.Empty;
            }
        }

        public async Task<IReadOnlyCollection<string>> SearchAsync(
            string? providerId = null,
            string query = "",
            int maxResults = 5,
            CancellationToken ct = default)
        {
            var provider = string.IsNullOrEmpty(providerId)
                ? GetDefaultProvider()
                : TryGetProvider(providerId, out var p) ? p : null;

            if (provider == null)
            {
                throw new InvalidOperationException("No knowledge provider available.");
            }

            if (!await provider.IsAvailableAsync(ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Provider '{provider.Id}' is not available.");
            }

            return await provider.SearchAsync(query, maxResults, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Dynamically loads all IKnowledgeProvider implementations from an external assembly.
        /// Each discovered type is instantiated via its parameterless constructor and registered.
        /// Types that require constructor parameters must be registered manually via RegisterProvider.
        /// </summary>
        public int LoadFromAssembly(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}", assemblyPath);

            var assembly = Assembly.LoadFrom(assemblyPath);
            var providerType = typeof(IKnowledgeProvider);
            var count = 0;

            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.IsAbstract && !type.IsInterface && providerType.IsAssignableFrom(type))
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor == null)
                        continue; // skip types that require DI — register those manually

                    if (Activator.CreateInstance(type) is IKnowledgeProvider instance)
                    {
                        RegisterProvider(instance);
                        count++;
                    }
                }
            }

            return count;
        }
    }
}