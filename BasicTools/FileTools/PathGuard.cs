namespace Valaiorp.BasicTools.FileTools
{
    /// <summary>
    /// Validates file and directory paths to prevent directory traversal attacks.
    /// Set <see cref="SandboxBase"/> to restrict all operations to a specific directory.
    /// </summary>
    public static class PathGuard
    {
        /// <summary>
        /// When set, every resolved path must reside within this directory.
        /// Leave null to allow any path while still blocking ".." traversal sequences.
        /// </summary>
        public static string? SandboxBase { get; set; }

        /// <summary>
        /// Validates and fully resolves a raw path.
        /// Throws <see cref="UnauthorizedAccessException"/> on traversal or sandbox violation.
        /// Throws <see cref="ArgumentException"/> when the path is null or empty.
        /// </summary>
        public static string Validate(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                throw new ArgumentException("File path is required.");

            if (rawPath.Contains("..", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Path traversal ('..') is not permitted.");

            var full = Path.GetFullPath(rawPath);

            if (SandboxBase is not null)
            {
                var sandboxFull = Path.GetFullPath(SandboxBase)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!full.StartsWith(sandboxFull, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException(
                        "Access denied: path resolves outside the allowed base directory.");
            }

            return full;
        }
    }
}
