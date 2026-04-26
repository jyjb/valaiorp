namespace Valaiorp.Tools.Enhanced.Components
{
    using Valaiorp.BasicTools.FileTools;

    public sealed class FileReader : IFileReader
    {
        private readonly JsonTool _jsonTool;
        private readonly JsonlTool _jsonlTool;

        public FileReader()
        {
            _jsonTool = new JsonTool();
            _jsonlTool = new JsonlTool();
        }

        public async Task<string> ReadJsonAsync(string filePath, CancellationToken ct = default)
        {
            return await _jsonTool.ReadAsync(filePath, ct).ConfigureAwait(false);
        }

        public async Task<string> ReadJsonlAsync(string filePath, CancellationToken ct = default)
        {
            return await _jsonlTool.ReadAsync(filePath, ct).ConfigureAwait(false);
        }
    }
}