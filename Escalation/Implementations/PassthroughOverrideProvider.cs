namespace Valaiorp.Escalation.Implementations
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// No-op override provider. Returns IsOverridden = false and leaves action and parameters
    /// unchanged. Use this as the default when runtime parameter overrides are not needed.
    /// </summary>
    public sealed class PassthroughOverrideProvider : IOverrideProvider
    {
        public Task<OverrideResult> OverrideAsync(
            IExecutionContext context,
            string action,
            string overrideReason,
            IDictionary<string, object>? newParameters = null,
            CancellationToken ct = default)
            => Task.FromResult(new OverrideResult(
                isOverridden: false,
                overrideId: null,
                originalAction: action,
                newAction: action,
                newParameters: newParameters));
    }
}
