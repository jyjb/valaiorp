namespace Valaiorp.Configuration.Models
{
    public sealed class KnowledgeConfig
    {
        /// <summary>Provider for embedding generation (e.g. "openai", "anthropic", "ollama", "custom").</summary>
        public string EmbeddingProvider { get; set; } = string.Empty;

        /// <summary>Embedding model ID as the provider names it (e.g. "text-embedding-3-small").</summary>
        public string EmbeddingModel { get; set; } = string.Empty;

        public int EmbeddingDimension { get; set; } = 1536;
        public float SimilarityThreshold { get; set; } = 0.75f;
        public int MaxContextLength { get; set; } = 4096;
        public int MaxKnowledgeResults { get; set; } = 5;
    }
}