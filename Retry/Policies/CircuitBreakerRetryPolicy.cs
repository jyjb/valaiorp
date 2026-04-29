namespace Valaiorp.Retry.Policies
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    /// <summary>
    /// Trips after <see cref="FailureThreshold"/> consecutive failures on the same
    /// circuit key (session + tool) and holds the circuit open for <see cref="ResetTimeout"/>.
    /// Thread-safe: all state mutations on a <see cref="CircuitState"/> are lock-guarded.
    /// </summary>
    public sealed class CircuitBreakerRetryPolicy : IRetryPolicy
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;

        // Key: "{sessionId}:{toolId}" – shared across requests in the same session to the same tool.
        private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();

        public int FailureThreshold => _failureThreshold;
        public TimeSpan ResetTimeout => _resetTimeout;

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
            // Key is stable across requests for the same logical resource.
            // Pull the tool hint from metadata if the executor placed it there; fall back to session.
            var toolHint = context.Metadata.TryGetValue("_circuitKey", out var k) && k is string s ? s : string.Empty;
            var circuitId = $"{context.SessionId}:{toolHint}";

            var state = _circuitStates.GetOrAdd(circuitId, _ => new CircuitState());

            lock (state)
            {
                if (state.IsOpen)
                {
                    if (DateTimeOffset.UtcNow - state.LastFailureTime > _resetTimeout)
                        state.Reset();
                    else
                        return Task.FromResult(false); // circuit still open — don't retry
                }

                if (!result.IsSuccess)
                {
                    state.FailureCount++;
                    state.LastFailureTime = DateTimeOffset.UtcNow;

                    if (state.FailureCount >= _failureThreshold)
                    {
                        state.IsOpen = true;
                        return Task.FromResult(false); // tripped — stop retrying
                    }
                }
                else
                {
                    state.Reset();
                }

                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Manually resets the circuit for a given session/tool combination.
        /// Useful in tests and admin endpoints.
        /// </summary>
        public void Reset(string sessionId, string toolId = "")
            => _circuitStates.TryRemove($"{sessionId}:{toolId}", out _);

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
