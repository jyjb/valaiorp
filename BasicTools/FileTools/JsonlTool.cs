namespace Valaiorp.BasicTools.FileTools
{
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class JsonlTool : IFileTool
    {
        public string Id => "jsonl-tool";
        public string Name => "JSONL Tool";
        public string Description => "Reads and writes JSON Lines files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".jsonl" }
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
                    await WriteAsync(filePath, parameters.GetString("content"), ct).ConfigureAwait(false);
                    return ToolResult.Ok();
                }

                return ToolResult.BadRequest(new { Message = $"Unknown operation '{operation}'. Use: read, write." });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        public async Task<string> ReadAsync(string filePath, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.");
            var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                try { JsonDocument.Parse(line); }
                catch (JsonException ex) { throw new InvalidDataException("File contains an invalid JSON line.", ex); }
            }
            return string.Join(Environment.NewLine, lines);
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            foreach (var line in content.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { JsonDocument.Parse(line); }
                catch (JsonException ex) { throw new InvalidDataException("Content contains an invalid JSON line.", ex); }
            }
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}
