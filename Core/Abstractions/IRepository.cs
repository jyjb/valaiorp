using Valaiorp.Core.Entities;

namespace Valaiorp.Core.Abstractions
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
        Task<T> UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(string id, CancellationToken ct = default);
    }
}