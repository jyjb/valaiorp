namespace Valaiorp.Escalation.Implementations
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Escalation.Contracts;

    /// <summary>
    /// Prompts an operator at the console to approve or reject a high-risk action.
    /// Suitable for interactive CLI / bot-operator scenarios.
    /// Times out and auto-rejects if no response is received within <see cref="Timeout"/>.
    /// </summary>
    public sealed class ConsoleApprovalProvider : IApprovalProvider
    {
        public TimeSpan Timeout { get; }

        public ConsoleApprovalProvider(TimeSpan? timeout = null)
            => Timeout = timeout ?? TimeSpan.FromMinutes(5);

        public async Task<ApprovalResult> RequestApprovalAsync(
            IExecutionContext context,
            string action,
            string? description = null,
            IDictionary<string, object>? metadata = null,
            CancellationToken ct = default)
        {
            Console.WriteLine($"\n[HITL] Approval required");
            Console.WriteLine($"  Context : {context.Id}");
            Console.WriteLine($"  Action  : {action}");
            if (!string.IsNullOrWhiteSpace(description))
                Console.WriteLine($"  Details : {description}");
            Console.Write("  Approve? [Y/N]: ");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(Timeout);

            try
            {
                var line = await Task.Run(() => Console.ReadLine(), linked.Token).ConfigureAwait(false);
                if (string.Equals(line?.Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    return ApprovalResult.Approved("console-operator");

                return ApprovalResult.Rejected("console-operator", "Operator rejected via console");
            }
            catch (OperationCanceledException)
            {
                return ApprovalResult.Rejected("console-timeout",
                    $"No operator response within {Timeout}; auto-rejected for safety");
            }
        }
    }
}
