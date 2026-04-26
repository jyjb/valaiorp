namespace Valaiorp.Core.Contracts
{
    public sealed class PromptContext
    {
        /// <summary>Static persona / role instructions sent as the system turn.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The runtime task or question from the user / calling agent.</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>Chunks retrieved from a knowledge provider (RAG).</summary>
        public IReadOnlyList<string> RagContext { get; set; } = [];

        /// <summary>Retrieved entries from short/long-term memory.</summary>
        public IReadOnlyList<string> MemoryContext { get; set; } = [];

        /// <summary>Prior conversation turns for multi-turn sessions.</summary>
        public IReadOnlyList<ConversationTurn> ConversationHistory { get; set; } = [];

        /// <summary>Named variables substituted into prompt templates (e.g. {AgentName}).</summary>
        public IReadOnlyDictionary<string, string> Variables { get; set; }
            = new Dictionary<string, string>();
    }
}
