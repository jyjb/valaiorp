namespace Valaiorp.Core.Enums
{
    /// <summary>
    /// Controls how much the AI is allowed to act within an AI Workflow, AI Agent, or Agentic workflow.
    /// Has no effect on IRPA workflows (AI is never invoked).
    /// </summary>
    public enum AiParticipation
    {
        /// <summary>AI reads context and produces insights/logs — no execution decisions made.</summary>
        ObserveOnly,

        /// <summary>AI proposes next steps; a human or approval gate must confirm before execution.</summary>
        ObserveAndSuggest,

        /// <summary>AI observes and acts autonomously — executes its decisions without approval.</summary>
        ObserveAndReact
    }
}
