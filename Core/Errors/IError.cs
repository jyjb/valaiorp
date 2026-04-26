namespace Valaiorp.Core.Errors
{
    public interface IError
    {
        string Code { get; }
        string Message { get; }
        Exception? InnerException { get; }
        IDictionary<string, object>? Metadata { get; }
    }
}