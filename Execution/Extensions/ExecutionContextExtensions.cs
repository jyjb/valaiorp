namespace Valaiorp.Execution.Extensions
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Models;

    public static class ExecutionContextExtensions
    {
        public static WorkflowExecutionContext AsWorkflowContext(
            this IExecutionContext context,
            ExecutionMode mode = ExecutionMode.Sequential,
            bool isAgentic = false)
        {
            return new WorkflowExecutionContext(context)
            {
                Mode = mode,
                IsAgentic = isAgentic
            };
        }
    }
}
