namespace Valaiorp.Core.Contracts
{
    public interface IExecutionResult
    {
        string Id { get; }
        string ContextId { get; }
        bool IsSuccess { get; }
        string? ErrorMessage { get; }
        Exception? Exception { get; }
        IReadOnlyCollection<IExecutionStep> ExecutedSteps { get; }
        IDictionary<string, object> Outputs { get; }
        TimeSpan ExecutionTime { get; }
    }
}