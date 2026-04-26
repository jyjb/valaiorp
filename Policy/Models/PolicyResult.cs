namespace Valaiorp.Policy.Models
{
    public sealed class PolicyResult
    {
        public bool IsAllowed { get; set; }
        public string? Reason { get; set; }
        public IDictionary<string, object>? Metadata { get; set; }

        public PolicyResult(bool isAllowed, string? reason = null, IDictionary<string, object>? metadata = null)
        {
            IsAllowed = isAllowed;
            Reason = reason;
            Metadata = metadata;
        }

        public static PolicyResult Allowed() => new(true);
        public static PolicyResult Denied(string reason) => new(false, reason);
    }
}