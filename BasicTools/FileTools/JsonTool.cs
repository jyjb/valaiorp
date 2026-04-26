namespace Valaiorp.BasicTools.FileTools
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class JsonTool : IFileTool
    {
        public string Id => "json-tool";
        public string Name => "JSON Tool";
        public string Description => "Reads and writes JSON files.";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "SupportedExtensions", new[] { ".json" } }
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            string input,
            CancellationToken ct = default)
        {
            try
            {
                var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return ToolResult.BadRequest(new { Message = "Invalid input format. Use: read|filePath or write|filePath|content" });

                var operation = parts[0].Trim().ToLower();
                var filePath  = parts[1].Trim();

                if (operation == "read")
                    return ToolResult.Ok(new { Content = await ReadAsync(filePath, ct).ConfigureAwait(false) });

                if (operation == "write" && parts.Length >= 3)
                {
                    await WriteAsync(filePath, parts[2].Trim(), ct).ConfigureAwait(false);
                    return ToolResult.Ok();
                }

                return ToolResult.BadRequest(new { Message = "Invalid operation or input format." });
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
