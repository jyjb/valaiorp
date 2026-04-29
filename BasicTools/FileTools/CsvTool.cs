namespace Valaiorp.BasicTools.FileTools
{
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class CsvTool : IFileTool
    {
        public string Id => "csv-tool";
        public string Name => "CSV Tool";
        public string Description => "Reads and writes CSV files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".csv" },
            ["Delimiter"] = ","
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
                    var raw     = parameters.GetString("content");
                    var content = TryConvertJsonToCsv(raw) ?? raw;
                    await WriteAsync(filePath, content, ct).ConfigureAwait(false);
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
            return await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }

        // Detects a JSON array and converts it to CSV; returns null if input is not a JSON array.
        private static string? TryConvertJsonToCsv(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.TrimStart();
            if (!trimmed.StartsWith('[')) return null;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

                var rows = doc.RootElement.EnumerateArray()
                    .Where(r => r.ValueKind == JsonValueKind.Object)
                    .ToList();
                if (rows.Count == 0) return string.Empty;

                var headers = rows
                    .SelectMany(r => r.EnumerateObject().Select(p => p.Name))
                    .Distinct()
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));

                foreach (var row in rows)
                {
                    var props = row.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                    var values = headers.Select(h =>
                    {
                        if (!props.TryGetValue(h, out var v)) return string.Empty;
                        var text = v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString();
                        return CsvEscape(text);
                    });
                    sb.AppendLine(string.Join(",", values));
                }

                return sb.ToString();
            }
            catch { return null; }
        }

        private static string CsvEscape(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
    }
}
