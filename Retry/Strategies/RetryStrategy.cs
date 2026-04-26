namespace Valaiorp.Retry.Strategies
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    public sealed class RetryStrategy : IRetryStrategy
    {
        private readonly IRetryPolicy _retryPolicy;

        public RetryStrategy(IRetryPolicy retryPolicy)
        {
            _retryPolicy = retryPolicy;
        }

        public async Task<IExecutionResult> ExecuteWithRetryAsync(
            Func<IExecutionContext, CancellationToken, Task<IExecutionResult>> operation,
            IExecutionContext context,
            CancellationToken ct = default)
        {
            int attemptNumber = 0;
            IExecutionResult? lastResult = null;

            while (true)
            {
                attemptNumber++;
                try
                {
                    lastResult = await operation(context, ct).ConfigureAwait(false);
                    if (lastResult.IsSuccess || !await _retryPolicy.ShouldRetryAsync(context, lastResult, attemptNumber, ct).ConfigureAwait(false))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastResult = new ExecutionResult(
                        context.Id,
                        context.Id,
                        false,
                        ex.Message,
                        ex);
                }
            }

            return lastResult!;
        }
    }
}