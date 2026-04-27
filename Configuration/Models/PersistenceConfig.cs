namespace Valaiorp.Configuration.Models
{
    public sealed class PersistenceConfig
    {
        /// <summary>Directory for JSONL execution logs. Always written to regardless of SQL config.</summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>Directory for JSONL queue files. Used by the default JsonlWorkQueue.</summary>
        public string QueueDirectory { get; set; } = "queues";

        /// <summary>
        /// Optional SQL connection string. When set, host app should call
        /// services.AddSqlPersistence(factory) to also log and queue via SQL.
        /// Leave empty to run entirely file-backed (the default).
        /// </summary>
        public string? ConnectionString { get; set; }
    }
}
