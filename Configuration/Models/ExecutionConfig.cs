namespace Valaiorp.Configuration.Models
{
    using Valaiorp.Core.Enums;

    public sealed class ExecutionConfig
    {
        public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxRetries { get; set; } = 3;
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerResetTime { get; set; } = TimeSpan.FromSeconds(30);
    }
}