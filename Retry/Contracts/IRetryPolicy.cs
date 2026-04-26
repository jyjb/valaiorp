namespace Valaiorp.Retry.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IRetryPolicy
    {
        Task<bool> ShouldRetryAsync(
            IExecutionContext context,
            IExecutionResult result,
            int attemptNumber,
            CancellationToken ct = default);
    }
}