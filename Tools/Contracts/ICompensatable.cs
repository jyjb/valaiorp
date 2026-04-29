namespace Valaiorp.Tools.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Opt-in interface for tools that can undo their own side-effects.
    ///
    /// When a tool implements <see cref="ICompensatable"/>, the TransactionManager
    /// calls <see cref="CompensateAsync"/> during rollback instead of only flipping an in-memory
    /// status flag. This enables real undo for file writes, API calls, database inserts, etc.
    ///
    /// Contract:
    ///   • <see cref="CompensateAsync"/> receives the same outputs the tool produced so it can
    ///     identify exactly what was created/changed (e.g., a file path, a record ID).
    ///   • The method MUST be idempotent — it may be called more than once on the same outputs.
    ///   • If compensation itself fails, throw; the caller will surface the error.
    /// </summary>
    public interface ICompensatable
    {
        Task CompensateAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> stepOutputs,
            CancellationToken ct = default);
    }
}
