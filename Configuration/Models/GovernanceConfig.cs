namespace Valaiorp.Configuration.Models
{
    public sealed class GovernanceConfig
    {
        public bool EnableAuditLogging { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableRateLimiting { get; set; } = true;
        public int MaxRequestsPerSecond { get; set; } = 100;
        public bool EnableContentModeration { get; set; } = true;
        public string[]? BannedKeywords { get; set; } = Array.Empty<string>();
    }
}