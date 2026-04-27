namespace Valaiorp.BasicTools.FolderTools
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Helpers;

    public enum FolderAction { Create, Delete, List, Copy, Move, Exists }

    public sealed class FolderTool : ITool
    {
        public string Id => "folder-tool";
        public string Name => "Folder Tool";
        public string Description => "Folder operations. Parameters: action (create|delete|list|copy|move|exists), path, destPath (copy/move), recursive (delete), pattern (list).";
        public ToolType Type => ToolType.Native;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedActions"] = Enum.GetNames(typeof(FolderAction))
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var actionStr = parameters.GetString("action");
                if (!Enum.TryParse<FolderAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown action '{actionStr}'. Use: create, delete, list, copy, move, exists." });

                var path      = parameters.GetString("path");
                var destPath  = parameters.GetString("destPath");
                var recursive = parameters.GetBool("recursive");
                var pattern   = parameters.GetString("pattern", "*");

                return action switch
                {
                    FolderAction.Create => Create(path),
                    FolderAction.Delete => Delete(path, recursive),
                    FolderAction.List   => List(path, pattern),
                    FolderAction.Copy   => await CopyAsync(path, destPath, ct).ConfigureAwait(false),
                    FolderAction.Move   => Move(path, destPath),
                    FolderAction.Exists => Exists(path),
                    _                  => ToolResult.BadRequest(new { Message = $"Unsupported action: {action}" })
                };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        private static ToolResult Create(string path) { Directory.CreateDirectory(path); return ToolResult.Created(new { Path = path }); }

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

        private static async Task<ToolResult> CopyAsync(string source, string dest, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dest)) return ToolResult.BadRequest(new { Message = "'destPath' is required for copy." });
            if (!Directory.Exists(source)) return ToolResult.NotFound(source);
            await CopyDirAsync(source, dest, ct).ConfigureAwait(false);
            return ToolResult.Ok(new { Source = source, Destination = dest });
        }

        private static async Task CopyDirAsync(string src, string dst, CancellationToken ct)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                ct.ThrowIfCancellationRequested();
                await using var s = File.OpenRead(file);
                await using var d = File.Create(Path.Combine(dst, Path.GetFileName(file)));
                await s.CopyToAsync(d, ct).ConfigureAwait(false);
            }
            foreach (var dir in Directory.GetDirectories(src))
                await CopyDirAsync(dir, Path.Combine(dst, Path.GetFileName(dir)), ct).ConfigureAwait(false);
        }

        private static ToolResult Move(string source, string dest)
        {
            if (string.IsNullOrWhiteSpace(dest)) return ToolResult.BadRequest(new { Message = "'destPath' is required for move." });
            if (!Directory.Exists(source)) return ToolResult.NotFound(source);
            Directory.Move(source, dest);
            return ToolResult.Ok(new { Source = source, Destination = dest });
        }

        private static ToolResult Exists(string path) =>
            ToolResult.Ok(new { Path = path, Exists = Directory.Exists(path) });
    }
}
