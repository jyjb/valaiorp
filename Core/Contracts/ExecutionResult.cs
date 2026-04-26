namespace Valaiorp.Core.Contracts
{
    public sealed class ExecutionResult : IExecutionResult
    {
        public string Id { get; }
        public string ContextId { get; }
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }
        public IReadOnlyCollection<IExecutionStep> ExecutedSteps { get; } = Array.Empty<IExecutionStep>();
        public IDictionary<string, object> Outputs { get; }
        public TimeSpan ExecutionTime { get; } = TimeSpan.Zero;

        public ExecutionResult(
            string id,
            string contextId,
            bool isSuccess,
            string? errorMessage = null,
            Exception? exception = null,
            IDictionary<string, object>? outputs = null)
        {
            Id = id;
            ContextId = contextId;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            Outputs = outputs ?? new Dictionary<string, object>();
        }
    }
}
