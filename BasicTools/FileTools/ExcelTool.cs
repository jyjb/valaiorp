namespace Valaiorp.BasicTools.FileTools
{
    using System.IO.Compression;
    using System.Text;
    using System.Text.Json;
    using System.Xml.Linq;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    // Reads and writes .xlsx using Open XML (System.IO.Compression + System.Xml.Linq).
    // No external NuGet packages required.
    public sealed class ExcelTool : IFileTool
    {
        private static readonly XNamespace Ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R  = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public string Id => "excel-tool";
        public string Name => "Excel Tool";
        public string Description => "Reads and writes .xlsx files using Open XML. Parameters: operation (read|write|getsheets|readcell|writecell), filePath, sheetName, data (write), cellRef (readcell/writecell), value (writecell).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedExtensions"] = new[] { ".xlsx" }
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
                    "read"      => ReadWorkbook(filePath, parameters.GetString("sheetName")),
                    "getsheets" => GetSheets(filePath),
                    "readcell"  => ReadCell(filePath, parameters.GetString("sheetName"), parameters.GetString("cellRef")),
                    "write"     => await WriteAsync(filePath, parameters.GetString("sheetName", "Sheet1"), parameters.Get<object>("data"), ct),
                    "writecell" => UpdateCell(filePath, parameters.GetString("sheetName"), parameters.GetString("cellRef"), parameters.GetString("value")),
                    _ => ToolResult.BadRequest(new { Message = $"Unknown operation '{op}'. Use: read, write, getsheets, readcell, writecell." })
                };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        // ── IFileTool (programmatic API) ─────────────────────────────────────────

        public Task<string> ReadAsync(string filePath, CancellationToken ct = default)
        {
            var result = ReadWorkbook(filePath, null);
            return Task.FromResult(JsonSerializer.Serialize(result.Results));
        }

        public async Task WriteAsync(string filePath, string content, CancellationToken ct = default)
        {
            var data = ParseJson2DArray(content);
            await CreateXlsxAsync(filePath, "Sheet1", data, ct).ConfigureAwait(false);
        }

        // ── Operations ───────────────────────────────────────────────────────────

        private ToolResult ReadWorkbook(string filePath, string? sheetFilter)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            using var zip   = ZipFile.OpenRead(filePath);
            var shared      = ReadSharedStrings(zip);
            var sheetMap    = BuildSheetMap(zip);
            var sheets      = new List<object>();

            foreach (var (name, path) in sheetMap)
            {
                if (sheetFilter != null && !name.Equals(sheetFilter, StringComparison.OrdinalIgnoreCase)) continue;
                sheets.Add(new { name, rows = ReadSheetData(zip, path, shared) });
            }

            return ToolResult.Ok(sheetFilter != null && sheets.Count == 1
                ? (object)new { rows = ((dynamic)sheets[0]).rows }
                : new { sheets });
        }

        private ToolResult GetSheets(string filePath)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            using var zip = ZipFile.OpenRead(filePath);
            return ToolResult.Ok(new { sheets = BuildSheetMap(zip).Select(s => s.name).ToArray() });
        }

        private ToolResult ReadCell(string filePath, string sheetName, string cellRef)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            cellRef = cellRef.ToUpperInvariant();
            using var zip  = ZipFile.OpenRead(filePath);
            var shared     = ReadSharedStrings(zip);
            var sheetMap   = BuildSheetMap(zip);
            var (_, path)  = sheetMap.FirstOrDefault(s => s.name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
            if (path == null) return ToolResult.NotFound($"Sheet '{sheetName}'");

            var entry = zip.GetEntry(path);
            if (entry == null) return ToolResult.NotFound(path);
            using var s = entry.Open();
            var doc  = XDocument.Load(s);
            var cell = doc.Descendants(Ss + "c").FirstOrDefault(c =>
                string.Equals(c.Attribute("r")?.Value, cellRef, StringComparison.OrdinalIgnoreCase));

            return ToolResult.Ok(new { cellRef, value = cell == null ? null : ResolveCellValue(cell, shared) });
        }

        private async Task<ToolResult> WriteAsync(string filePath, string sheetName, object? data, CancellationToken ct)
        {
            var json  = data is string s ? s : JsonSerializer.Serialize(data);
            var rows  = ParseJson2DArray(json);
            await CreateXlsxAsync(filePath, sheetName, rows, ct).ConfigureAwait(false);
            return ToolResult.Ok(new { filePath, sheetName, rows = rows.Count });
        }

        private ToolResult UpdateCell(string filePath, string sheetName, string cellRef, string newValue)
        {
            if (!File.Exists(filePath)) return ToolResult.NotFound(filePath);
            cellRef = cellRef.ToUpperInvariant();

            var entries = ReadZip(filePath);
            var sheetMap = BuildSheetMapFromEntries(entries);
            var (_, sheetPath) = sheetMap.FirstOrDefault(s => s.name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheetPath == null) return ToolResult.NotFound($"Sheet '{sheetName}'");

            var shared = ReadSharedStringsFromEntries(entries);
            var doc    = XDocument.Parse(Encoding.UTF8.GetString(entries[sheetPath]));
            bool isNum = double.TryParse(newValue, out _);

            var cell = doc.Descendants(Ss + "c").FirstOrDefault(c =>
                string.Equals(c.Attribute("r")?.Value, cellRef, StringComparison.OrdinalIgnoreCase));

            if (cell != null)
            {
                cell.Attributes("t").Remove();
                cell.Elements(Ss + "v").Remove();
                if (isNum) { cell.Add(new XElement(Ss + "v", newValue)); }
                else       { int idx = EnsureShared(shared, newValue); cell.SetAttributeValue("t", "s"); cell.Add(new XElement(Ss + "v", idx)); }
            }
            else
            {
                var (col, rowNum) = ParseCellRef(cellRef);
                var row = doc.Descendants(Ss + "row").FirstOrDefault(r => r.Attribute("r")?.Value == rowNum.ToString());
                if (row == null)
                {
                    row = new XElement(Ss + "row", new XAttribute("r", rowNum));
                    var sd = doc.Root!.Element(Ss + "sheetData")!;
                    var prev = sd.Elements(Ss + "row").LastOrDefault(r => int.Parse(r.Attribute("r")!.Value) < rowNum);
                    if (prev == null) sd.AddFirst(row); else prev.AddAfterSelf(row);
                }
                XElement newCell = isNum
                    ? new XElement(Ss + "c", new XAttribute("r", cellRef), new XElement(Ss + "v", newValue))
                    : new XElement(Ss + "c", new XAttribute("r", cellRef), new XAttribute("t", "s"), new XElement(Ss + "v", EnsureShared(shared, newValue)));
                row.Add(newCell);
            }

            entries[sheetPath] = SerializeXml(doc);
            if (shared.Count > 0) entries["xl/sharedStrings.xml"] = Encoding.UTF8.GetBytes(BuildSharedStringsXml(shared));
            WriteZip(filePath, entries);
            return ToolResult.Ok(new { cellRef, value = newValue, sheetName });
        }

        // ── XML building ─────────────────────────────────────────────────────────

        private static async Task CreateXlsxAsync(string filePath, string sheetName, List<List<string>> data, CancellationToken ct)
        {
            var strings = new List<string>();
            var strIdx  = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in data)
                foreach (var cell in row)
                    if (!string.IsNullOrEmpty(cell) && !double.TryParse(cell, out _) && !strIdx.ContainsKey(cell))
                    { strIdx[cell] = strings.Count; strings.Add(cell); }

            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                AddEntry(zip, "[Content_Types].xml",        ContentTypesXml(strings.Count > 0));
                AddEntry(zip, "_rels/.rels",                RootRels);
                AddEntry(zip, "xl/workbook.xml",            WorkbookXml(sheetName));
                AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(strings.Count > 0));
                AddEntry(zip, "xl/worksheets/sheet1.xml",   SheetXml(data, strIdx));
                AddEntry(zip, "xl/styles.xml",              StylesXml);
                if (strings.Count > 0) AddEntry(zip, "xl/sharedStrings.xml", BuildSharedStringsXml(strings));
            }
            ms.Seek(0, SeekOrigin.Begin);
            await using var fs = new FileStream(filePath, FileMode.Create);
            await ms.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        private static string ContentTypesXml(bool hasSS) =>
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>{(hasSS ? """<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>""" : "")}</Types>""";

        private const string RootRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

        private static string WorkbookXml(string name) =>
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Esc(name)}" sheetId="1" r:id="rId1"/></sheets></workbook>""";

        private static string WorkbookRels(bool hasSS) =>
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>{(hasSS ? """<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>""" : "")}</Relationships>""";

        private const string StylesXml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";

        private static string SheetXml(List<List<string>> data, Dictionary<string, int> strIdx)
        {
            var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
            for (int r = 0; r < data.Count; r++)
            {
                sb.Append($"""<row r="{r + 1}">""");
                for (int c = 0; c < data[r].Count; c++)
                {
                    var v  = data[r][c];
                    if (string.IsNullOrEmpty(v)) continue;
                    var cr = ColLetter(c + 1) + (r + 1);
                    if (double.TryParse(v, out _))
                        sb.Append($"""<c r="{cr}"><v>{Esc(v)}</v></c>""");
                    else
                        sb.Append($"""<c r="{cr}" t="s"><v>{strIdx[v]}</v></c>""");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildSharedStringsXml(List<string> strings)
        {
            var sb = new StringBuilder($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{strings.Count}" uniqueCount="{strings.Count}">""");
            foreach (var s in strings) sb.Append($"<si><t>{Esc(s)}</t></si>");
            sb.Append("</sst>");
            return sb.ToString();
        }

        // ── Read helpers ─────────────────────────────────────────────────────────

        private static List<(string name, string path)> BuildSheetMap(ZipArchive zip)
        {
            var wbEntry = zip.GetEntry("xl/workbook.xml");
            if (wbEntry == null) return new();
            using var ws = wbEntry.Open();
            var wb = XDocument.Load(ws);

            var rels = new Dictionary<string, string>();
            var re = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (re != null) { using var rs = re.Open(); foreach (var rel in XDocument.Load(rs).Root!.Elements()) rels[rel.Attribute("Id")!.Value] = ResolveRel("xl/", rel.Attribute("Target")!.Value); }

            var result = new List<(string, string)>();
            foreach (var sh in wb.Descendants(Ss + "sheet"))
            {
                var name = sh.Attribute("name")?.Value ?? "";
                var rId  = sh.Attribute(R + "id")?.Value ?? "";
                result.Add((name, rels.TryGetValue(rId, out var p) ? p : $"xl/worksheets/sheet{result.Count + 1}.xml"));
            }
            return result;
        }

        private static List<(string name, string path)> BuildSheetMapFromEntries(Dictionary<string, byte[]> entries)
        {
            if (!entries.TryGetValue("xl/workbook.xml", out var wb)) return new();
            var wbDoc = XDocument.Parse(Encoding.UTF8.GetString(wb));
            var rels  = new Dictionary<string, string>();
            if (entries.TryGetValue("xl/_rels/workbook.xml.rels", out var rb))
                foreach (var rel in XDocument.Parse(Encoding.UTF8.GetString(rb)).Root!.Elements())
                    rels[rel.Attribute("Id")!.Value] = ResolveRel("xl/", rel.Attribute("Target")!.Value);
            var result = new List<(string, string)>();
            foreach (var sh in wbDoc.Descendants(Ss + "sheet"))
            {
                var name = sh.Attribute("name")?.Value ?? "";
                var rId  = sh.Attribute(R + "id")?.Value ?? "";
                result.Add((name, rels.TryGetValue(rId, out var p) ? p : $"xl/worksheets/sheet{result.Count + 1}.xml"));
            }
            return result;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return new();
            using var s = e.Open();
            return XDocument.Load(s).Root!.Elements(Ss + "si").Select(si => si.Descendants(Ss + "t").Aggregate("", (a, t) => a + t.Value)).ToList();
        }

        private static List<string> ReadSharedStringsFromEntries(Dictionary<string, byte[]> entries)
        {
            if (!entries.TryGetValue("xl/sharedStrings.xml", out var b)) return new();
            return XDocument.Parse(Encoding.UTF8.GetString(b)).Root!.Elements(Ss + "si").Select(si => si.Descendants(Ss + "t").Aggregate("", (a, t) => a + t.Value)).ToList();
        }

        private static List<List<string>> ReadSheetData(ZipArchive zip, string path, List<string> shared)
        {
            var entry = zip.GetEntry(path);
            if (entry == null) return new();
            using var s = entry.Open();
            var doc  = XDocument.Load(s);
            var rows = new List<List<string>>();
            foreach (var row in doc.Descendants(Ss + "row"))
            {
                var rowData = new List<string>();
                int lastCol = 0;
                foreach (var cell in row.Elements(Ss + "c"))
                {
                    var (col, _) = ParseCellRef(cell.Attribute("r")?.Value ?? "A1");
                    int colIdx   = ColIndex(col) - 1;
                    while (lastCol < colIdx) { rowData.Add(""); lastCol++; }
                    rowData.Add(ResolveCellValue(cell, shared));
                    lastCol++;
                }
                rows.Add(rowData);
            }
            return rows;
        }

        private static string ResolveCellValue(XElement cell, List<string> shared)
        {
            var type = cell.Attribute("t")?.Value ?? "";
            var v    = cell.Element(Ss + "v")?.Value ?? "";
            return type switch
            {
                "s"         => int.TryParse(v, out int i) && i < shared.Count ? shared[i] : v,
                "inlineStr" => cell.Descendants(Ss + "t").FirstOrDefault()?.Value ?? "",
                "b"         => v == "1" ? "TRUE" : "FALSE",
                _           => v
            };
        }

        private static int EnsureShared(List<string> shared, string value)
        {
            int idx = shared.IndexOf(value);
            if (idx < 0) { idx = shared.Count; shared.Add(value); }
            return idx;
        }

        // ── Zip helpers ──────────────────────────────────────────────────────────

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

        // ── Utilities ────────────────────────────────────────────────────────────

        private static (string col, int row) ParseCellRef(string r)
        {
            int i = 0;
            while (i < r.Length && char.IsLetter(r[i])) i++;
            return i == 0 ? ("A", 1) : (r[..i], int.TryParse(r[i..], out int n) ? n : 1);
        }

        private static int ColIndex(string col) { int v = 0; foreach (char c in col.ToUpperInvariant()) v = v * 26 + (c - 'A' + 1); return v; }

        private static string ColLetter(int idx) { var s = ""; while (idx > 0) { idx--; s = (char)('A' + idx % 26) + s; idx /= 26; } return s; }

        private static string ResolveRel(string baseDir, string target)
        {
            if (target.StartsWith('/')) return target.TrimStart('/');
            return baseDir + target.TrimStart('/');
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
