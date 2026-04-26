namespace Valaiorp.Policy.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Policy.Models;

    public interface IPolicyEngine
    {
        Task<PolicyResult> EvaluatePreExecutionAsync(
            IExecutionContext context,
            CancellationToken ct = default);

        Task<PolicyResult> EvaluatePostExecutionAsync(
            IExecutionResult result,
            CancellationToken ct = default);

        void AddRule(PolicyRule rule);
        void RemoveRule(string ruleId);
    }
}