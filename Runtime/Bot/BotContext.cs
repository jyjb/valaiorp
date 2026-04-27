namespace Valaiorp.Runtime.Bot
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class BotContext : IBotContext
    {
        public string       BotId        { get; init; } = string.Empty;
        public string       InstanceId   { get; }       = Guid.NewGuid().ToString("N");
        public string       MachineName  { get; }       = Environment.MachineName;
        public WorkflowType WorkflowType { get; init; } = WorkflowType.Irpa;
        public DateTimeOffset StartedAt  { get; }       = DateTimeOffset.UtcNow;
    }
}
