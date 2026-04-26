namespace Valaiorp.Core.Enums
{
    public enum AutonomyMode
    {
        FullyDeterministic,    // 0.0: No agentic behavior, strict rules
        ControlledHybrid,      // 0.3: Limited agentic decisions
        AssistedAgentic,       // 0.7: Mostly agentic with guardrails
        FullyAgentic           // 1.0: Full agentic freedom
    }
}