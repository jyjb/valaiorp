namespace Valaiorp.Tools.Governance
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Mandatory governance chokepoint consulted for every tool call routed through
    /// <see cref="Resolvers.ToolResolver.ExecuteToolAsync(string, IExecutionContext, IReadOnlyDictionary{string, object}, System.Threading.CancellationToken)"/>.
    /// A gate authorizes (or rejects) a tool call before the tool executes.
    /// </summary>
    public interface IExecutionGate
    {
        /// <summary>
        /// Decides whether the given tool call may execute. Implementations should fail closed:
        /// any exception propagates and prevents execution.
        /// </summary>
        /// <param name="toolId">The resolved tool identifier.</param>
        /// <param name="context">The execution context for this call.</param>
        /// <param name="parameters">The resolved tool parameters.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<GateDecision> AuthorizeAsync(
            string toolId,
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default);
    }
}
