namespace Valaiorp.Core.Contracts
{
    using Valaiorp.Core.Enums;

    /// <summary>Identity and runtime metadata for a single BotWorker instance.</summary>
    public interface IBotContext
    {
        /// <summary>Logical bot name — shared across all instances of the same bot type.</summary>
        string BotId { get; }

        /// <summary>Unique ID for this specific process instance.</summary>
        string InstanceId { get; }

        string MachineName { get; }

        WorkflowType WorkflowType { get; }

        DateTimeOffset StartedAt { get; }
    }
}
