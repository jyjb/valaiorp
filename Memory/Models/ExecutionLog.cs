namespace Valaiorp.Memory.Models
{
    using Valaiorp.Core.Entities;
    using Valaiorp.Core.Contracts;

    public sealed class ExecutionLog : BaseEntity
    {
        public string ContextId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}