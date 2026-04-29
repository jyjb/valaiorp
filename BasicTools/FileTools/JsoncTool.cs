namespace Valaiorp.BasicTools.FileTools
{
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class JsoncTool : IFileTool
    {
        public string Id => "jsonc-tool";
        public string Name => "JSONC Tool";
        public string Description => "Reads and writes JSONC (JSON with Comments) files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".jsonc", ".json5" }
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
            var raw = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var stripped = StripComments(raw);
            try { JsonDocument.Parse(stripped); }
            catch (JsonException ex) { throw new InvalidDataException("File is not valid JSONC.", ex); }
            return raw;
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            var stripped = StripComments(content);
            try { JsonDocument.Parse(stripped); }
            catch (JsonException ex) { throw new InvalidDataException("Content is not valid JSONC.", ex); }
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }

        // Strips // line comments and /* block comments */ from JSON text,
        // correctly ignoring comment-like sequences inside string literals.
        public static string StripComments(string src)
        {
            var sb  = new StringBuilder(src.Length);
            int i   = 0;
            bool inString = false;

            while (i < src.Length)
            {
                char c = src[i];

                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length)
                    {
                        sb.Append(src[++i]); // escaped char — skip
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                    i++;
                    continue;
                }

                // Line comment
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    i += 2;
                    while (i < src.Length && src[i] != '\n') i++;
                    continue;
                }

                // Block comment
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i += 2; // skip closing */
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }
    }
}
