namespace Valaiorp.BasicTools.FileTools
{
    using Valaiorp.Tools.Contracts;

    public interface IFileTool : ITool
    {
        Task<string> ReadAsync(string filePath, CancellationToken ct = default);
        Task WriteAsync(string filePath, string content, CancellationToken ct = default);
    }
}
