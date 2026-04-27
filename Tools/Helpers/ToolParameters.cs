namespace Valaiorp.Tools.Helpers
{
    public static class ToolParameters
    {
        public static string GetString(this IReadOnlyDictionary<string, object> p, string key, string fallback = "")
            => p.TryGetValue(key, out var v) ? v?.ToString() ?? fallback : fallback;

        public static bool GetBool(this IReadOnlyDictionary<string, object> p, string key, bool fallback = false)
            => p.TryGetValue(key, out var v)
                ? v is bool b ? b : bool.TryParse(v?.ToString(), out var r) ? r : fallback
                : fallback;

        public static int GetInt(this IReadOnlyDictionary<string, object> p, string key, int fallback = 0)
            => p.TryGetValue(key, out var v)
                ? v is int i ? i : int.TryParse(v?.ToString(), out var r) ? r : fallback
                : fallback;

        public static T? Get<T>(this IReadOnlyDictionary<string, object> p, string key)
            => p.TryGetValue(key, out var v) && v is T typed ? typed : default;

        public static IReadOnlyDictionary<string, object> Empty { get; } =
            new Dictionary<string, object>();
    }
}
