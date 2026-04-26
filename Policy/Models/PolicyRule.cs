namespace Valaiorp.Policy.Models
{
    using Valaiorp.Core.Contracts;

    public abstract class PolicyRule
    {
        public string Id { get; protected set; } = Guid.NewGuid().ToString("N");
        public string Name { get; protected set; } = string.Empty;
        public string Description { get; protected set; } = string.Empty;
        public bool IsEnforced { get; set; } = true;

        public abstract Task<PolicyResult> EvaluatePreExecutionAsync(
            IExecutionContext context,
            CancellationToken ct = default);

        public abstract Task<PolicyResult> EvaluatePostExecutionAsync(
            IExecutionResult result,
            CancellationToken ct = default);
    }
}