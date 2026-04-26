namespace Valaiorp.Memory.Models
{
    using Valaiorp.Core.Entities;

    public sealed class FeedbackEntry : BaseEntity
    {
        public string ContextId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Rating { get; set; } // 1-5
        public string? Comment { get; set; }
        public IDictionary<string, object>? Metadata { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}