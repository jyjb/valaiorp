namespace Valaiorp.Tools.Enhanced.Components
{
    using Valaiorp.Core.Contracts;

    public interface IFileReader
    {
        Task<string> ReadJsonAsync(string filePath, CancellationToken ct = default);
        Task<string> ReadJsonlAsync(string filePath, CancellationToken ct = default);
    }
}