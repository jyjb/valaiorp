namespace Valaiorp.Retry.Policies
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    public sealed class MaxAttemptsRetryPolicy : IRetryPolicy
    {
        private readonly int _maxAttempts;

        public MaxAttemptsRetryPolicy(int maxAttempts = 3)
        {
            _maxAttempts = maxAttempts;
        }

        public Task<bool> ShouldRetryAsync(
            IExecutionContext context,
            IExecutionResult result,
            int attemptNumber,
            CancellationToken ct = default)
        {
            return Task.FromResult(attemptNumber < _maxAttempts && !result.IsSuccess);
        }
    }
}