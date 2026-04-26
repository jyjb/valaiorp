namespace Valaiorp.Policy.Engines
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Policy.Contracts;
    using Valaiorp.Policy.Models;

    public sealed class PolicyEngine : IPolicyEngine
    {
        private readonly ConcurrentDictionary<string, PolicyRule> _rules = new();

        public void AddRule(PolicyRule rule)
        {
            _rules.TryAdd(rule.Id, rule);
        }

        public void RemoveRule(string ruleId)
        {
            _rules.TryRemove(ruleId, out _);
        }

        public async Task<PolicyResult> EvaluatePreExecutionAsync(
            IExecutionContext context,
            CancellationToken ct = default)
        {
            foreach (var rule in _rules.Values)
            {
                if (!rule.IsEnforced)
                {
                    continue;
                }

                var result = await rule.EvaluatePreExecutionAsync(context, ct).ConfigureAwait(false);
                if (!result.IsAllowed)
                {
                    return result;
                }
            }

            return PolicyResult.Allowed();
        }

        public async Task<PolicyResult> EvaluatePostExecutionAsync(
            IExecutionResult result,
            CancellationToken ct = default)
        {
            foreach (var rule in _rules.Values)
            {
                if (!rule.IsEnforced)
                {
                    continue;
                }

                var policyResult = await rule.EvaluatePostExecutionAsync(result, ct).ConfigureAwait(false);
                if (!policyResult.IsAllowed)
                {
                    return policyResult;
                }
            }

            return PolicyResult.Allowed();
        }
    }
}