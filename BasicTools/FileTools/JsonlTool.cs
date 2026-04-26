namespace Valaiorp.BasicTools.FileTools
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public sealed class JsonlTool : IFileTool
    {
        public string Id => "jsonl-tool";
        public string Name => "JSONL Tool";
        public string Description => "Reads and writes JSON Lines files.";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "SupportedExtensions", new[] { ".jsonl" } }
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
            var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
            foreach (var line in lines)
            {
                try { JsonDocument.Parse(line); }
                catch (JsonException ex) { throw new InvalidDataException($"Line is not valid JSON: {line}", ex); }
            }
            return string.Join(Environment.NewLine, lines);
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            foreach (var line in content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { JsonDocument.Parse(line); }
                catch (JsonException ex) { throw new InvalidDataException($"Line is not valid JSON: {line}", ex); }
            }
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}
