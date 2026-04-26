namespace Valaiorp.Retry.Policies
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    public sealed class CircuitBreakerRetryPolicy : IRetryPolicy
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();

        public CircuitBreakerRetryPolicy(int failureThreshold = 5, TimeSpan resetTimeout = default)
        {
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout == default ? TimeSpan.FromSeconds(30) : resetTimeout;
        }

        public Task<bool> ShouldRetryAsync(
            IExecutionContext context,
            IExecutionResult result,
            int attemptNumber,
            CancellationToken ct = default)
        {
            var circuitId = context.Id;
            var state = _circuitStates.GetOrAdd(circuitId, _ => new CircuitState());

            if (state.IsOpen)
            {
                if (DateTimeOffset.UtcNow - state.LastFailureTime > _resetTimeout)
                {
                    // Reset the circuit
                    state.Reset();
                }
                else
                {
                    return Task.FromResult(false);
                }
            }

            if (!result.IsSuccess)
            {
                state.FailureCount++;
                state.LastFailureTime = DateTimeOffset.UtcNow;

                if (state.FailureCount >= _failureThreshold)
                {
                    state.IsOpen = true;
                    return Task.FromResult(false);
                }
            }
            else
            {
                state.Reset();
            }

            return Task.FromResult(true);
        }

        private sealed class CircuitState
        {
            public int FailureCount { get; set; }
            public DateTimeOffset LastFailureTime { get; set; }
            public bool IsOpen { get; set; }

            public void Reset()
            {
                FailureCount = 0;
                IsOpen = false;
            }
        }
    }
}