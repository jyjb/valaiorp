namespace Valaiorp.BasicTools.FileTools
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class TxtTool : IFileTool
    {
        public string Id => "txt-tool";
        public string Name => "TXT Tool";
        public string Description => "Reads and writes plain text files. Parameters: operation (read|write|append), filePath, content (write/append only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".txt", ".log" }
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

                var content = parameters.GetString("content");

                if (operation == "write")  { await WriteAsync(filePath, content, ct).ConfigureAwait(false); return ToolResult.Ok(); }
                if (operation == "append") { await File.AppendAllTextAsync(filePath, content, ct).ConfigureAwait(false); return ToolResult.Ok(); }

                return ToolResult.BadRequest(new { Message = $"Unknown operation '{operation}'. Use: read, write, append." });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        public async Task<string> ReadAsync(string filePath, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.");
            return await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}
