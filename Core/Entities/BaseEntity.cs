namespace Valaiorp.Core.Entities
{
    public abstract class BaseEntity
    {
        public string Id { get; protected set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; protected set; }
    }
}