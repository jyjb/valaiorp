namespace Valaiorp.Core.Contracts
{
    using Valaiorp.Core.Enums;

    public sealed class QueueReport
    {
        public string          QueueId               { get; init; } = string.Empty;
        public DateTimeOffset  GeneratedAt           { get; init; } = DateTimeOffset.UtcNow;

        public int TotalItems   { get; init; }
        public int Pending      { get; init; }
        public int InProgress   { get; init; }
        public int Completed    { get; init; }
        public int Failed       { get; init; }
        public int DeadLetter   { get; init; }

        public TimeSpan AverageProcessingTime { get; init; }
        public TimeSpan? TotalElapsedTime     { get; init; }

        public IReadOnlyList<QueueRun>  Runs        { get; init; } = [];
        public IReadOnlyList<IWorkItem> FailedItems { get; init; } = [];
        public IReadOnlyList<IWorkItem> AllItems    { get; init; } = [];

        public double SuccessRate => TotalItems == 0 ? 0 :
            Math.Round((double)Completed / TotalItems * 100, 2);

        public IReadOnlyDictionary<string, int> FailuresByExceptionType =>
            FailedItems
                .Where(i => i.ExceptionType != null)
                .GroupBy(i => i.ExceptionType!)
                .ToDictionary(g => g.Key, g => g.Count());
    }
}
