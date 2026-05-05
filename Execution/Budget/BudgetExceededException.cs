namespace Valaiorp.Execution.Budget
{
    public enum BudgetKind { ToolCalls, Tokens, ExecutionTime }

    public sealed class BudgetExceededException : Exception
    {
        public BudgetKind Kind { get; }
        public long Used { get; }
        public long Limit { get; }

        public BudgetExceededException(BudgetKind kind, long used, long limit)
            : base($"Budget exceeded — {kind}: used {used}, limit {limit}.")
        {
            Kind  = kind;
            Used  = used;
            Limit = limit;
        }
    }
}
