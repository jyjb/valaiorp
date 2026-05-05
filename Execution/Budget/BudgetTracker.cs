namespace Valaiorp.Execution.Budget
{
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Enforces an AgentBudget at runtime. Create one per workflow execution.
    /// Thread-safe for concurrent tool calls.
    /// </summary>
    public sealed class BudgetTracker
    {
        private readonly AgentBudget _budget;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _toolCallsUsed;
        private int _tokensUsed;

        public BudgetTracker(AgentBudget budget) => _budget = budget;

        public int ToolCallsUsed => _toolCallsUsed;
        public int TokensUsed    => _tokensUsed;
        public TimeSpan Elapsed  => _stopwatch.Elapsed;

        /// <summary>
        /// Increments the tool-call counter and throws if MaxToolCalls is exceeded.
        /// Call this immediately before invoking a tool.
        /// </summary>
        public void RecordToolCall()
        {
            var used = Interlocked.Increment(ref _toolCallsUsed);
            if (_budget.MaxToolCalls.HasValue && used > _budget.MaxToolCalls.Value)
                throw new BudgetExceededException(BudgetKind.ToolCalls, used, _budget.MaxToolCalls.Value);
        }

        /// <summary>
        /// Adds <paramref name="tokens"/> to the running total and throws if MaxTokens is exceeded.
        /// Feed this from LLM response token counts (e.g. TokenUsage.TotalTokens).
        /// </summary>
        public void RecordTokens(int tokens)
        {
            var used = Interlocked.Add(ref _tokensUsed, tokens);
            if (_budget.MaxTokens.HasValue && used > _budget.MaxTokens.Value)
                throw new BudgetExceededException(BudgetKind.Tokens, used, _budget.MaxTokens.Value);
        }

        /// <summary>
        /// Throws if MaxExecutionTime has elapsed. Call at the start of each step.
        /// </summary>
        public void CheckTime()
        {
            if (_budget.MaxExecutionTime.HasValue && _stopwatch.Elapsed > _budget.MaxExecutionTime.Value)
                throw new BudgetExceededException(
                    BudgetKind.ExecutionTime,
                    (long)_stopwatch.Elapsed.TotalMilliseconds,
                    (long)_budget.MaxExecutionTime.Value.TotalMilliseconds);
        }
    }
}
