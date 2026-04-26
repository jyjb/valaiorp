namespace Valaiorp.Retry.Policies
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;

        public ExponentialBackoffRetryPolicy(
            int maxAttempts = 5,
            TimeSpan initialDelay = default,
            TimeSpan maxDelay = default)
        {
            _maxAttempts = maxAttempts;
            _initialDelay = initialDelay == default ? TimeSpan.FromMilliseconds(100) : initialDelay;
            _maxDelay = maxDelay == default ? TimeSpan.FromSeconds(10) : maxDelay;
        }

        public async Task<bool> ShouldRetryAsync(
            IExecutionContext context,
            IExecutionResult result,
            int attemptNumber,
            CancellationToken ct = default)
        {
            if (attemptNumber >= _maxAttempts || result.IsSuccess)
            {
                return false;
            }

            var delay = TimeSpan.FromTicks(Math.Min(
                _initialDelay.Ticks * (long)Math.Pow(2, attemptNumber),
                _maxDelay.Ticks));

            await Task.Delay(delay, ct).ConfigureAwait(false);
            return true;
        }
    }
}