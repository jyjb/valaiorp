namespace Valaiorp.Memory.Implementations
{
    using System.Text.Json;
    using Valaiorp.Memory.Contracts;

    /// <summary>
    /// File-backed short-term memory. All key-value pairs are persisted to a single JSON file
    /// so data survives process restarts. A SemaphoreSlim serialises concurrent access.
    /// </summary>
    public sealed class JsonlShortTermMemory : IShortTermMemory
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public JsonlShortTermMemory(string directory)
        {
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "short-term.json");
        }

        public async Task SetAsync(string key, object value, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var store = await ReadStoreAsync().ConfigureAwait(false);
                store[key] = JsonSerializer.SerializeToElement(value, _opts);
                await WriteStoreAsync(store).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var store = await ReadStoreAsync().ConfigureAwait(false);
                return store.TryGetValue(key, out var el) ? el.Deserialize<T>(_opts) : default;
            }
            finally { _lock.Release(); }
        }

        public async Task RemoveAsync(string key, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var store = await ReadStoreAsync().ConfigureAwait(false);
                if (store.Remove(key))
                    await WriteStoreAsync(store).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        public async Task ClearAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try { await WriteStoreAsync([]).ConfigureAwait(false); }
            finally { _lock.Release(); }
        }

        public async Task<bool> ContainsKeyAsync(string key, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var store = await ReadStoreAsync().ConfigureAwait(false);
                return store.ContainsKey(key);
            }
            finally { _lock.Release(); }
        }

        private async Task<Dictionary<string, JsonElement>> ReadStoreAsync()
        {
            if (!File.Exists(_filePath))
                return [];
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _opts) ?? [];
        }

        private Task WriteStoreAsync(Dictionary<string, JsonElement> store)
        {
            var json = JsonSerializer.Serialize(store, _opts);
            return File.WriteAllTextAsync(_filePath, json);
        }
    }
}
