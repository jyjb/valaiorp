namespace Valaiorp.Retry.Policies
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Retry.Contracts;

    public sealed class CompositeRetryPolicy : IRetryPolicy
    {
        private readonly IRetryPolicy[] _policies;

        public CompositeRetryPolicy(params IRetryPolicy[] policies)
        {
            _policies = policies;
        }

        public async Task<bool> ShouldRetryAsync(
            IExecutionContext context,
            IExecutionResult result,
            int attemptNumber,
            CancellationToken ct = default)
        {
            foreach (var policy in _policies)
            {
                if (!await policy.ShouldRetryAsync(context, result, attemptNumber, ct).ConfigureAwait(false))
                {
                    return false;
                }
            }
            return true;
        }
    }
}