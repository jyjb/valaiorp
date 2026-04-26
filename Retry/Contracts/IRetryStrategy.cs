namespace Valaiorp.Retry.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IRetryStrategy
    {
        Task<IExecutionResult> ExecuteWithRetryAsync(
            Func<IExecutionContext, CancellationToken, Task<IExecutionResult>> operation,
            IExecutionContext context,
            CancellationToken ct = default);
    }
}