namespace Valaiorp.BasicTools.FileTools
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class JsonTool : IFileTool
    {
        public string Id => "json-tool";
        public string Name => "JSON Tool";
        public string Description => "Reads and writes JSON files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".json" }
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var operation = parameters.GetString("operation", "read");
                var filePath  = parameters.GetString("filePath");

                if (string.IsNullOrWhiteSpace(filePath))
                    return ToolResult.BadRequest(new { Message = "Parameter 'filePath' is required." });

                if (operation == "read")
                    return ToolResult.Ok(new { Content = await ReadAsync(filePath, ct).ConfigureAwait(false) });

                if (operation == "write")
                {
                    var content = parameters.GetString("content");
                    await WriteAsync(filePath, content, ct).ConfigureAwait(false);
                    return ToolResult.Ok();
                }

                return ToolResult.BadRequest(new { Message = $"Unknown operation '{operation}'. Use: read, write." });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        public async Task<string> ReadAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");
            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            try { JsonDocument.Parse(content); }
            catch (JsonException ex) { throw new InvalidDataException("File is not valid JSON.", ex); }
            return content;
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            try { JsonDocument.Parse(content); }
            catch (JsonException ex) { throw new InvalidDataException("Content is not valid JSON.", ex); }
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}
