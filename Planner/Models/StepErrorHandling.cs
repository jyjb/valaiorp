namespace Valaiorp.Planner.Models
{
    public sealed class StepErrorHandling
    {
        public RetryPolicy? RetryPolicy { get; set; }
        public StepFallback? Fallback { get; set; }
    }

    public sealed class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 3;
        public BackoffPolicy? Backoff { get; set; }
        public CircuitBreaker? CircuitBreaker { get; set; }
    }

    public sealed class BackoffPolicy
    {
        public int InitialDelayMs { get; set; } = 100;
        public int MaxDelayMs { get; set; } = 10000;
        public double Multiplier { get; set; } = 2.0;
    }

    public sealed class CircuitBreaker
    {
        public int FailureThreshold { get; set; } = 5;
        public int ResetTimeoutMs { get; set; } = 30000;
    }

    public sealed class StepFallback
    {
        public bool Enabled { get; set; }
        public string? Action { get; set; }
    }
}
