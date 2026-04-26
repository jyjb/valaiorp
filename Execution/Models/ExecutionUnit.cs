namespace Valaiorp.Execution.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Planner.Models;

    public sealed class ExecutionUnit
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string ContextId { get; set; } = string.Empty;
        public Plan Plan { get; set; } = new();
        public TaskGraph Graph { get; set; } = new();
        public IExecutionContext Context { get; set; } = null!;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAt { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
        public Exception? Exception { get; set; }
    }

    public enum ExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        RolledBack
    }
}
