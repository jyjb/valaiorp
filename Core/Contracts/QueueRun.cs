namespace Valaiorp.Core.Contracts
{
    /// <summary>Tracks a single bot session against a queue (start → end).</summary>
    public sealed class QueueRun
    {
        public string         RunId       { get; init; } = Guid.NewGuid().ToString("N");
        public string         QueueId     { get; init; } = string.Empty;
        public string         BotId       { get; init; } = string.Empty;
        public string         MachineName { get; init; } = Environment.MachineName;
        public DateTimeOffset  StartedAt  { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? EndedAt    { get; set;  }
        public bool            IsActive   => EndedAt == null;
    }
}
