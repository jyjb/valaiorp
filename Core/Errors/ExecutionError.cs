namespace Valaiorp.Core.Errors
{
    public sealed class ExecutionError : IError
    {
        public string Code { get; }
        public string Message { get; }
        public Exception? InnerException { get; }
        public IDictionary<string, object>? Metadata { get; }

        public ExecutionError(string code, string message, Exception? innerException = null, IDictionary<string, object>? metadata = null)
        {
            Code = code;
            Message = message;
            InnerException = innerException;
            Metadata = metadata;
        }
    }
}