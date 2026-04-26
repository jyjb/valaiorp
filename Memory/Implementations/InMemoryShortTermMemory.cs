namespace Valaiorp.Memory.Implementations
{
    using System.Collections.Concurrent;
    using Valaiorp.Memory.Contracts;

    public sealed class InMemoryShortTermMemory : IShortTermMemory
    {
        private readonly ConcurrentDictionary<string, object> _memory = new();

        public Task SetAsync(string key, object value, CancellationToken ct = default)
        {
            _memory.AddOrUpdate(key, value, (_, _) => value);
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            if (_memory.TryGetValue(key, out var value))
            {
                return Task.FromResult(value is T result ? result : default);
            }
            return Task.FromResult<T?>(default);
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _memory.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken ct = default)
        {
            _memory.Clear();
            return Task.CompletedTask;
        }

        public Task<bool> ContainsKeyAsync(string key, CancellationToken ct = default)
        {
            return Task.FromResult(_memory.ContainsKey(key));
        }
    }
}
