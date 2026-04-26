namespace Valaiorp.Memory.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Models;
    public interface ILongTermMemory
    {
        Task StoreAsync(string contextId, IExecutionContext context, CancellationToken ct = default);
        Task<IExecutionContext?> RetrieveAsync(string contextId, CancellationToken ct = default);
        Task StoreLogAsync(ExecutionLog log, CancellationToken ct = default);
        Task<IReadOnlyList<ExecutionLog>> RetrieveLogsAsync(string contextId, CancellationToken ct = default);
        Task StoreFeedbackAsync(FeedbackEntry feedback, CancellationToken ct = default);
        Task<IReadOnlyList<FeedbackEntry>> RetrieveFeedbackAsync(string contextId, CancellationToken ct = default);
    }
}