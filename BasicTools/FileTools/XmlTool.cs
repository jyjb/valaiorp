namespace Valaiorp.BasicTools.FileTools
{
    using System.Xml;
    using System.IO;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class XmlTool : IFileTool
    {
        public string Id => "xml-tool";
        public string Name => "XML Tool";
        public string Description => "Reads and writes XML files. Parameters: operation (read|write), filePath, content (write only).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".xml" }
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
            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            try
            {
                var settings = new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit };
                using var reader = XmlReader.Create(new StringReader(content), settings);
                while (reader.Read()) { }
            }
            catch (XmlException ex) { throw new InvalidDataException("File is not valid XML.", ex); }
            return content;
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            filePath = PathGuard.Validate(filePath);
            try
            {
                var settings = new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit };
                using var reader = XmlReader.Create(new StringReader(content), settings);
                while (reader.Read()) { }
            }
            catch (XmlException ex) { throw new InvalidDataException("Content is not valid XML.", ex); }
            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
        }
    }
}
