namespace Valaiorp.BasicTools.FileTools
{
    using System.IO.Compression;
    using System.Text;
    using System.Text.Json;
    using System.Xml.Linq;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    // Reads and writes .docx using Open XML (System.IO.Compression + System.Xml.Linq).
    // No external NuGet packages required.
    public sealed class WordTool : IFileTool
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public string Id => "word-tool";
        public string Name => "Word Tool";
        public string Description => "Reads and writes .docx files using Open XML. Parameters: operation (read|write|append|addheading|addtable), filePath, content, level (addheading 1-3), data (addtable 2D array).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".docx" }
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var op       = parameters.GetString("operation", "read");
                var filePath = parameters.GetString("filePath");

                if (string.IsNullOrWhiteSpace(filePath))
                    return ToolResult.BadRequest(new { Message = "Parameter 'filePath' is required." });

                return op switch
                {
                    "read"       => ReadDocument(filePath),
                    "write"      => await WriteDocAsync(filePath, parameters.GetString("content"), ct),
                    "append"     => ModifyDoc(filePath, body => AddParagraphs(body, parameters.GetString("content"))),
                    "addheading" => ModifyDoc(filePath, body => AddHeading(body, parameters.GetInt("level", 1), parameters.GetString("content"))),
                    "addtable"   => ModifyDoc(filePath, body => AddTable(body, parameters.Get<object>("data"))),
                    _ => ToolResult.BadRequest(new { Message = $"Unknown operation '{op}'. Use: read, write, append, addheading, addtable." })
                };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        // ── IFileTool (programmatic API) ─────────────────────────────────────────

        public Task<string> ReadAsync(string filePath, CancellationToken ct = default)
        {
            var r = ReadDocument(filePath);
            return Task.FromResult(r.Results?.ToString() ?? "");
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
            => await CreateDocxAsync(filePath, content, ct).ConfigureAwait(false);

        // ── Operations ───────────────────────────────────────────────────────────

        private ToolResult ReadDocument(string filePath)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            using var zip   = ZipFile.OpenRead(filePath);
            var entry       = zip.GetEntry("word/document.xml");
            if (entry == null) return ToolResult.Error("word/document.xml not found.");
            using var s     = entry.Open();
            var doc         = XDocument.Load(s);
            var paragraphs  = doc.Descendants(W + "p")
                .Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            return ToolResult.Ok(new { text = string.Join("\n", paragraphs), paragraphs });
        }

        private async Task<ToolResult> WriteDocAsync(string filePath, string content, CancellationToken ct)
        {
            await CreateDocxAsync(filePath, content, ct).ConfigureAwait(false);
            return ToolResult.Ok(new { filePath, paragraphs = content.Split('\n').Length });
        }

        private ToolResult ModifyDoc(string filePath, Action<XElement> modifier)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            var entries = ReadZip(filePath);
            if (!entries.TryGetValue("word/document.xml", out var bytes))
                return ToolResult.Error("word/document.xml not found.");
            var doc  = XDocument.Parse(Encoding.UTF8.GetString(bytes));
            var body = doc.Root!.Element(W + "body")!;
            modifier(body);
            entries["word/document.xml"] = SerializeXml(doc);
            WriteZip(filePath, entries);
            return ToolResult.Ok(new { filePath });
        }

        // ── Body modifiers ───────────────────────────────────────────────────────

        private void AddParagraphs(XElement body, string content)
        {
            var sect = body.Element(W + "sectPr");
            foreach (var line in content.Split('\n'))
                InsertBefore(body, sect, BuildParagraph(line));
        }

        private void AddHeading(XElement body, int level, string text)
        {
            var sect = body.Element(W + "sectPr");
            InsertBefore(body, sect,
                new XElement(W + "p",
                    new XElement(W + "pPr",
                        new XElement(W + "pStyle", new XAttribute(W + "val", $"Heading{Math.Clamp(level, 1, 3)}"))),
                    new XElement(W + "r", new XElement(W + "t", text))));
        }

        private void AddTable(XElement body, object? data)
        {
            var json = data is string s ? s : JsonSerializer.Serialize(data);
            var rows = ParseJson2DArray(json);
            if (rows.Count == 0) return;
            var sect = body.Element(W + "sectPr");

            var tbl = new XElement(W + "tbl",
                new XElement(W + "tblPr",
                    new XElement(W + "tblStyle", new XAttribute(W + "val", "TableGrid")),
                    new XElement(W + "tblW", new XAttribute(W + "w", "0"), new XAttribute(W + "type", "auto"))));

            foreach (var row in rows)
            {
                var tr = new XElement(W + "tr");
                foreach (var cell in row)
                    tr.Add(new XElement(W + "tc",
                        new XElement(W + "p",
                            new XElement(W + "r",
                                new XElement(W + "t",
                                    new XAttribute(XNamespace.Xml + "space", "preserve"), cell)))));
                tbl.Add(tr);
            }
            InsertBefore(body, sect, tbl);
        }

        private static void InsertBefore(XElement body, XElement? sect, XElement element)
        {
            if (sect != null) sect.AddBeforeSelf(element);
            else body.Add(element);
        }

        private XElement BuildParagraph(string text) =>
            new(W + "p",
                new XElement(W + "r",
                    new XElement(W + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), text)));

        // ── File creation ────────────────────────────────────────────────────────

        private static async Task CreateDocxAsync(string filePath, string content, CancellationToken ct)
        {
            var sb = new StringBuilder();
            foreach (var line in content.Split('\n'))
                sb.Append($"""<w:p><w:r><w:t xml:space="preserve">{Esc(line)}</w:t></w:r></w:p>""");
            sb.Append("<w:sectPr/>");

            var docXml = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{sb}</w:body></w:document>""";

            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                AddEntry(zip, "[Content_Types].xml", ContentTypesXml);
                AddEntry(zip, "_rels/.rels",         RootRels);
                AddEntry(zip, "word/document.xml",   docXml);
                AddEntry(zip, "word/_rels/document.xml.rels", DocumentRels);
                AddEntry(zip, "word/styles.xml",     StylesXml);
            }
            ms.Seek(0, SeekOrigin.Begin);
            await using var fs = new FileStream(filePath, FileMode.Create);
            await ms.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        // ── Static XML constants ─────────────────────────────────────────────────

        private const string ContentTypesXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>""";
        private const string RootRels         = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""";
        private const string DocumentRels     = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";
        private const string StylesXml        = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style><w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="32"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="26"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="2"/></w:pPr><w:rPr><w:b/><w:sz w:val="24"/></w:rPr></w:style><w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:space="0" w:color="000000"/><w:left w:val="single" w:sz="4" w:space="0" w:color="000000"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="000000"/><w:right w:val="single" w:sz="4" w:space="0" w:color="000000"/><w:insideH w:val="single" w:sz="4" w:space="0" w:color="000000"/><w:insideV w:val="single" w:sz="4" w:space="0" w:color="000000"/></w:tblBorders></w:tblPr></w:style></w:styles>""";

        // ── Utilities ────────────────────────────────────────────────────────────

        private static Dictionary<string, byte[]> ReadZip(string path)
        {
            var d = new Dictionary<string, byte[]>();
            using var z = ZipFile.OpenRead(path);
            foreach (var e in z.Entries) { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); d[e.FullName] = ms.ToArray(); }
            return d;
        }

        private static void WriteZip(string path, Dictionary<string, byte[]> entries)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var z  = new ZipArchive(fs, ZipArchiveMode.Create);
            foreach (var (name, data) in entries) { var e = z.CreateEntry(name); using var s = e.Open(); s.Write(data); }
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            var e = zip.CreateEntry(name);
            using var s = e.Open();
            s.Write(Encoding.UTF8.GetBytes(content));
        }

        private static byte[] SerializeXml(XDocument doc)
        {
            using var ms = new MemoryStream();
            var settings = new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
            using (var w = System.Xml.XmlWriter.Create(ms, settings)) doc.Save(w);
            return ms.ToArray();
        }

        private static List<List<string>> ParseJson2DArray(string json)
        {
            try
            {
                return (JsonSerializer.Deserialize<List<List<JsonElement>>>(json) ?? new())
                    .Select(row => row.Select(c => c.ValueKind switch
                    {
                        JsonValueKind.String => c.GetString() ?? "",
                        JsonValueKind.Number => c.GetRawText(),
                        JsonValueKind.True   => "TRUE",
                        JsonValueKind.False  => "FALSE",
                        _                   => ""
                    }).ToList()).ToList();
            }
            catch { return new(); }
        }

        private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
