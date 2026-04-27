namespace Valaiorp.Core.Enums
{
    public enum PlannerType
    {
        None,

        /// <summary>Rule-based reactive planner — responds to context signals, no LLM.</summary>
        Reactive,

        /// <summary>Code-authored fixed plan — fully deterministic, no LLM. Used for IRPA.</summary>
        Deliberative,

        /// <summary>Hierarchical task decomposition — no LLM, driven by registered modules.</summary>
        Hierarchical,

        /// <summary>LLM generates and optionally revises the plan. Used for AI Workflow and AI Agent.</summary>
        LlmBased,

        /// <summary>LLM with full autonomy — dynamic re-planning, tool selection, sub-agent spawning. Used for Agentic.</summary>
        AutonomyAware,

        /// <summary>
        /// Externally supplied JSON plan — no planner runs at all.
        /// The Plan JSON is deserialized directly and executed as-is.
        /// Useful for developer testing, plan replay, and LLM-generated plans passed in from outside the runtime.
        /// </summary>
        Manual
    }
}
