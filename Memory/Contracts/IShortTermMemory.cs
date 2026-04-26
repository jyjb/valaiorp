namespace Valaiorp.Memory.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IShortTermMemory
    {
        Task SetAsync(string key, object value, CancellationToken ct = default);
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
        Task RemoveAsync(string key, CancellationToken ct = default);
        Task ClearAsync(CancellationToken ct = default);
        Task<bool> ContainsKeyAsync(string key, CancellationToken ct = default);
    }
}