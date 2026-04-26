namespace Valaiorp.BasicTools.FolderTools
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;

    public enum FolderAction { Create, Delete, List, Copy, Move, Exists }

    public sealed class FolderTool : ITool
    {
        public string Id => "folder-tool";
        public string Name => "Folder Tool";
        public string Description => "Folder operations: create, delete, list, copy, move, exists.";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "SupportedActions", Enum.GetNames(typeof(FolderAction)) },
            { "InputFormat", "action|path[|destPath|recursive]" }
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
                    return ToolResult.BadRequest(new { Message = "Invalid input. Use: create|path, delete|path[|recursive], list|path[|pattern], copy|src|dest, move|src|dest, exists|path" });

                if (!Enum.TryParse<FolderAction>(parts[0].Trim(), true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Invalid action: {parts[0].Trim()}" });

                return action switch
                {
                    FolderAction.Create => Create(parts[1].Trim()),
                    FolderAction.Delete => Delete(parts[1].Trim(), parts.Length > 2 && parts[2].Trim().Equals("recursive", StringComparison.OrdinalIgnoreCase)),
                    FolderAction.List   => List(parts[1].Trim(), parts.Length > 2 ? parts[2].Trim() : "*"),
                    FolderAction.Copy   => await CopyAsync(parts[1].Trim(), parts.Length > 2 ? parts[2].Trim() : null, ct).ConfigureAwait(false),
                    FolderAction.Move   => Move(parts[1].Trim(), parts.Length > 2 ? parts[2].Trim() : null),
                    FolderAction.Exists => Exists(parts[1].Trim()),
                    _                   => ToolResult.BadRequest(new { Message = $"Unsupported action: {action}" })
                };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        private static ToolResult Create(string path)
        {
            Directory.CreateDirectory(path);
            return ToolResult.Created(new { Path = path });
        }

        private static ToolResult Delete(string path, bool recursive)
        {
            if (!Directory.Exists(path)) return ToolResult.NotFound(path);
            Directory.Delete(path, recursive);
            return ToolResult.Ok(new { Deleted = path });
        }

        private static ToolResult List(string path, string pattern)
        {
            if (!Directory.Exists(path)) return ToolResult.NotFound(path);
            var entries = Directory.GetFileSystemEntries(path, pattern)
                .Select(e => new { Name = Path.GetFileName(e), FullPath = e, IsDirectory = Directory.Exists(e) })
                .ToArray();
            return ToolResult.Ok(new { Path = path, Entries = entries, Count = entries.Length });
        }

        private static async Task<ToolResult> CopyAsync(string source, string? dest, CancellationToken ct)
        {
            if (dest == null) return ToolResult.BadRequest(new { Message = "Destination path required: copy|src|dest" });
            if (!Directory.Exists(source)) return ToolResult.NotFound(source);
            await CopyDirectoryAsync(source, dest, ct).ConfigureAwait(false);
            return ToolResult.Ok(new { Source = source, Destination = dest });
        }

        private static async Task CopyDirectoryAsync(string source, string dest, CancellationToken ct)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                ct.ThrowIfCancellationRequested();
                await using var src = File.OpenRead(file);
                await using var dst = File.Create(Path.Combine(dest, Path.GetFileName(file)));
                await src.CopyToAsync(dst, ct).ConfigureAwait(false);
            }
            foreach (var dir in Directory.GetDirectories(source))
                await CopyDirectoryAsync(dir, Path.Combine(dest, Path.GetFileName(dir)), ct).ConfigureAwait(false);
        }

        private static ToolResult Move(string source, string? dest)
        {
            if (dest == null) return ToolResult.BadRequest(new { Message = "Destination path required: move|src|dest" });
            if (!Directory.Exists(source)) return ToolResult.NotFound(source);
            Directory.Move(source, dest);
            return ToolResult.Ok(new { Source = source, Destination = dest });
        }

        private static ToolResult Exists(string path) =>
            ToolResult.Ok(new { Path = path, Exists = Directory.Exists(path) });
    }
}
