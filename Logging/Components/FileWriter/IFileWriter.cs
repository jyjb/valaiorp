namespace Valaiorp.Tools.Enhanced.Components
{
    using Valaiorp.Core.Contracts;

    public interface IFileWriter
    {
        Task WriteJsonAsync(string filePath, string content, CancellationToken ct = default);
        Task WriteJsonlAsync(string filePath, string content, CancellationToken ct = default);
    }
}