namespace Valaiorp.Core.Errors
{
    public sealed class ValidationError : IError
    {
        public string Code { get; } = "VALIDATION_ERROR";
        public string Message { get; }
        public Exception? InnerException { get; } = null;
        public IDictionary<string, object>? Metadata { get; }

        public ValidationError(string message, IDictionary<string, object>? metadata = null)
        {
            Message = message;
            Metadata = metadata;
        }
    }
}