namespace Valaiorp.Escalation.Contracts
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Interface for override providers. Implement this in your host application
    /// to allow manual overrides of actions or parameters.
    /// </summary>
    public interface IOverrideProvider
    {
        /// <summary>
        /// Requests an override for an action in the given execution context.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="action">The action to override.</param>
        /// <param name="overrideReason">Reason for the override.</param>
        /// <param name="newParameters">Optional new parameters for the action.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Override result with details.</returns>
        Task<OverrideResult> OverrideAsync(
            IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Result of an override request.
    /// </summary>
    public sealed class OverrideResult
    {
        /// <summary>
        /// Whether the override was successful.
        /// </summary>
        public bool IsOverridden { get; set; }

        /// <summary>
        /// ID of the override (e.g., for auditing).
        /// </summary>
        public string? OverrideId { get; set; }

        /// <summary>
        /// The original action before override.
        /// </summary>
        public string? OriginalAction { get; set; }

        /// <summary>
        /// The new action after override.
        /// </summary>
        public string? NewAction { get; set; }

        /// <summary>
        /// New parameters for the action.
        /// </summary>
        public IDictionary<string, object>? NewParameters { get; set; }

        /// <summary>
        /// Timestamp of the override.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public OverrideResult(
            bool isOverridden,
            string? overrideId = null,
            string? originalAction = null,
            string? newAction = null,
            IDictionary<string, object>? newParameters = null)
        {
            IsOverridden = isOverridden;
            OverrideId = overrideId;
            OriginalAction = originalAction;
            NewAction = newAction;
            NewParameters = newParameters;
        }
    }
}