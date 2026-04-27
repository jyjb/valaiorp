namespace Valaiorp.Core.Contracts
{
    using Valaiorp.Core.Enums;

    public interface IWorkItem
    {
        string         ItemId           { get; }
        string         QueueId          { get; }
        string?        Reference        { get; }   // external key — invoice number, case ID, etc.
        string?        Tag              { get; }   // grouping / routing label
        int            Priority         { get; }
        int            AttemptCount     { get; }
        WorkItemStatus Status           { get; }
        string?        AssignedToBotId  { get; }
        DateTimeOffset  EnqueuedAt      { get; }
        DateTimeOffset? ScheduledAt     { get; }
        DateTimeOffset? StartedAt       { get; }
        DateTimeOffset? CompletedAt     { get; }
        string?        FailureReason    { get; }
        string?        ExceptionType    { get; }
        string?        ExceptionDetail  { get; }
        IDictionary<string, object>  Payload { get; }
        IDictionary<string, object>? Output  { get; }
    }

    public sealed class WorkItem : IWorkItem
    {
        public string         ItemId          { get; init; } = Guid.NewGuid().ToString("N");
        public string         QueueId         { get; init; } = string.Empty;
        public string?        Reference       { get; init; }
        public string?        Tag             { get; init; }
        public int            Priority        { get; init; } = 0;
        public int            AttemptCount    { get; set;  } = 0;
        public WorkItemStatus Status          { get; set;  } = WorkItemStatus.Pending;
        public string?        AssignedToBotId { get; set;  }
        public DateTimeOffset  EnqueuedAt     { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledAt    { get; init; }
        public DateTimeOffset? StartedAt      { get; set;  }
        public DateTimeOffset? CompletedAt    { get; set;  }
        public string?        FailureReason   { get; set;  }
        public string?        ExceptionType   { get; set;  }
        public string?        ExceptionDetail { get; set;  }
        public IDictionary<string, object>  Payload { get; init; } = new Dictionary<string, object>();
        public IDictionary<string, object>? Output  { get; set;  }
    }
}
