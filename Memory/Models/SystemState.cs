namespace Valaiorp.Memory.Models
{
    using Valaiorp.Core.Entities;

    public sealed class SystemState : BaseEntity
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public IDictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}