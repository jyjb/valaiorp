namespace Valaiorp.Core.Enums
{
    public enum WorkflowType
    {
        /// <summary>Pure automation, no AI. Tools/modules run a fixed, code-authored plan.</summary>
        Irpa,

        /// <summary>AI produces the execution plan; steps run deterministically after planning.</summary>
        AiWorkflow,

        /// <summary>AI plans and re-plans between steps based on intermediate results.</summary>
        AiAgent,

        /// <summary>Fully autonomous AI — dynamic tool selection, self-directed re-planning, sub-agent spawning.</summary>
        Agentic
    }
}
