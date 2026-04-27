namespace Valaiorp.BasicTools.FileTools
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class PsvTool : IFileTool
    {
        public string Id => "psv-tool";
        public string Name => "PSV Tool";
        public string Description => "Reads and writes PSV (Pipe-Separated Values) files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".psv" },
            ["Delimiter"] = "|"
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
            if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");
            return await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
            => await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
    }
}
