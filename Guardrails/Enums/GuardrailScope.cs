namespace Valaiorp.Guardrails.Enums
{
    public enum GuardrailScope
    {
        /// <summary>Evaluates user prompts and plan inputs before they reach the planner or LLM.</summary>
        Input,

        /// <summary>Evaluates LLM responses and execution outputs before they are committed.</summary>
        Output,

        /// <summary>Evaluates tool calls — validates the toolId and its input parameters.</summary>
        Tool,

        /// <summary>Runs for every evaluation scope (input, output, and tool).</summary>
        All
    }
}
