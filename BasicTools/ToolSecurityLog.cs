namespace Valaiorp.BasicTools
{
    using System.Text.Json;

    /// <summary>
    /// Writes security-relevant tool events to a dedicated JSONL audit log.
    /// Location: valaiorp-security.jsonl alongside the application binary.
    /// Failures are swallowed — security logging must never crash a tool call.
    /// </summary>
    internal static class ToolSecurityLog
    {
        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "valaiorp-security.jsonl");

        private static readonly object _lock = new();

        internal static void Write(string tool, string action, string contextId, object details)
        {
            try
            {
                var entry = JsonSerializer.Serialize(new
                {
                    ts      = DateTimeOffset.UtcNow,
                    tool,
                    action,
                    ctx     = contextId,
                    details
                });

                lock (_lock)
                    File.AppendAllText(LogPath, entry + Environment.NewLine);
            }
            catch { }
        }
    }
}
