namespace Valaiorp.Execution.Models
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Budget;

    public sealed class WorkflowExecutionContext : IExecutionContext
    {
        private readonly IExecutionContext _baseContext;
        private readonly Dictionary<string, object> _workflowState = new();

        public WorkflowExecutionContext(IExecutionContext baseContext)
        {
            _baseContext = baseContext;
        }

        public string Id => _baseContext.Id;
        public string SessionId => _baseContext.SessionId;
        public string UserId => _baseContext.UserId;
        public DateTimeOffset CreatedAt => _baseContext.CreatedAt;
        public DateTimeOffset? ExpiresAt => _baseContext.ExpiresAt;
        public IDictionary<string, object> Metadata => _baseContext.Metadata;
        public IReadOnlyCollection<IExecutionStep> Steps => _baseContext.Steps;
        public CancellationToken CancellationToken => _baseContext.CancellationToken;
        public PromptContext? Prompt => _baseContext.Prompt;

        public IDictionary<string, object> WorkflowState => _workflowState;
        public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
        public bool IsAgentic { get; set; }
        public AgentBudget? Budget { get; set; }
    }
}
