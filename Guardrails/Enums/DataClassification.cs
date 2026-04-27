namespace Valaiorp.Guardrails.Enums
{
    public enum DataClassification
    {
        /// <summary>No restrictions — safe for public consumption.</summary>
        Public,

        /// <summary>Internal company data — not for external sharing.</summary>
        Internal,

        /// <summary>Sensitive business data — restricted distribution.</summary>
        Confidential,

        /// <summary>Highly sensitive data (PII, credentials, secrets) — strict access controls.</summary>
        Restricted
    }
}
