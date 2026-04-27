namespace Valaiorp.Configuration.Models
{
    public sealed class GuardrailConfig
    {
        /// <summary>Detect and redact PII (email, phone, SSN, credit card, IP) from all content.</summary>
        public bool EnablePiiRedaction { get; set; } = false;

        /// <summary>Block prompt injection attempts in user inputs.</summary>
        public bool EnablePromptInjectionDetection { get; set; } = true;

        /// <summary>Block content containing any of the BannedKeywords entries.</summary>
        public bool EnableBannedKeywords { get; set; } = false;

        public string[] BannedKeywords { get; set; } = [];

        /// <summary>Block input content exceeding this character count. 0 = disabled.</summary>
        public int MaxInputLengthChars { get; set; } = 32_000;

        /// <summary>Block output content exceeding this character count. 0 = disabled.</summary>
        public int MaxOutputLengthChars { get; set; } = 32_000;

        /// <summary>
        /// When set, only these tool IDs may be invoked.
        /// Null means all tools are allowed (subject to DeniedToolIds).
        /// </summary>
        public string[]? AllowedToolIds { get; set; }

        /// <summary>Tool IDs that are always blocked, regardless of AllowedToolIds.</summary>
        public string[]? DeniedToolIds { get; set; }

        /// <summary>Tag content with a data-sensitivity classification and emit audit warnings.</summary>
        public bool EnableDataClassification { get; set; } = false;
    }
}
