namespace Valaiorp.Retry.Extensions
{
    using Valaiorp.Retry.Contracts;
    using Valaiorp.Retry.Policies;
    using Valaiorp.Retry.Strategies;

    public static class RetryPolicyExtensions
    {
        public static IRetryPolicy WithMaxAttempts(this IRetryPolicy policy, int maxAttempts)
        {
            return new CompositeRetryPolicy(policy, new MaxAttemptsRetryPolicy(maxAttempts));
        }

        public static IRetryPolicy WithCircuitBreaker(
            this IRetryPolicy policy,
            int failureThreshold = 5,
            TimeSpan resetTimeout = default)
        {
            return new CompositeRetryPolicy(policy, new CircuitBreakerRetryPolicy(failureThreshold, resetTimeout));
        }

        public static IRetryPolicy WithExponentialBackoff(
            this IRetryPolicy policy,
            int maxAttempts = 5,
            TimeSpan initialDelay = default,
            TimeSpan maxDelay = default)
        {
            return new CompositeRetryPolicy(policy, new ExponentialBackoffRetryPolicy(maxAttempts, initialDelay, maxDelay));
        }

        public static IRetryStrategy ToRetryStrategy(this IRetryPolicy policy)
        {
            return new RetryStrategy(policy);
        }
    }
}