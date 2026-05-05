namespace Valaiorp.Execution.Budget
{
    public sealed class AgentBudget
    {
        /// <summary>Maximum number of tool invocations allowed across the workflow run.</summary>
        public int? MaxToolCalls { get; init; }

        /// <summary>
        /// Maximum total tokens allowed. Tracked via BudgetTracker.RecordTokens —
        /// callers (e.g. AgentRuntime) must feed token counts from LLM responses.
        /// </summary>
        public int? MaxTokens { get; init; }

        /// <summary>Maximum wall-clock time allowed for the entire workflow execution.</summary>
        public TimeSpan? MaxExecutionTime { get; init; }
    }
}
