namespace Valaiorp.Tools.Enhanced.Components
{
    using Valaiorp.BasicTools.FileTools;

    public sealed class FileWriter : IFileWriter
    {
        private readonly JsonTool _jsonTool;
        private readonly JsonlTool _jsonlTool;

        public FileWriter()
        {
            _jsonTool = new JsonTool();
            _jsonlTool = new JsonlTool();
        }

        public async Task WriteJsonAsync(string filePath, string content, CancellationToken ct = default)
        {
            await _jsonTool.WriteAsync(filePath, content, ct).ConfigureAwait(false);
        }

        public async Task WriteJsonlAsync(string filePath, string content, CancellationToken ct = default)
        {
            await _jsonlTool.WriteAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}