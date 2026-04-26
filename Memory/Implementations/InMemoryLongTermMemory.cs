namespace Valaiorp.Memory.Implementations
{
    using System.Collections.Concurrent;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Memory.Models;

    public sealed class InMemoryLongTermMemory : ILongTermMemory
    {
        private readonly ConcurrentDictionary<string, IExecutionContext> _contexts = new();
        private readonly ConcurrentDictionary<string, List<ExecutionLog>> _logs = new();
        private readonly ConcurrentDictionary<string, List<FeedbackEntry>> _feedback = new();

        public Task StoreAsync(string contextId, IExecutionContext context, CancellationToken ct = default)
        {
            _contexts.AddOrUpdate(contextId, context, (_, _) => context);
            return Task.CompletedTask;
        }

        public Task<IExecutionContext?> RetrieveAsync(string contextId, CancellationToken ct = default)
        {
            return Task.FromResult(_contexts.TryGetValue(contextId, out var context) ? context : null);
        }

        public Task StoreLogAsync(ExecutionLog log, CancellationToken ct = default)
        {
            var logs = _logs.GetOrAdd(log.ContextId, _ => new List<ExecutionLog>());
            logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExecutionLog>> RetrieveLogsAsync(string contextId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ExecutionLog>>(
                _logs.TryGetValue(contextId, out var logs) ? logs.AsReadOnly() : Array.Empty<ExecutionLog>());
        }

        public Task StoreFeedbackAsync(FeedbackEntry feedback, CancellationToken ct = default)
        {
            var feedbacks = _feedback.GetOrAdd(feedback.ContextId, _ => new List<FeedbackEntry>());
            feedbacks.Add(feedback);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FeedbackEntry>> RetrieveFeedbackAsync(string contextId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<FeedbackEntry>>(
                _feedback.TryGetValue(contextId, out var feedbacks) ? feedbacks.AsReadOnly() : Array.Empty<FeedbackEntry>());
        }
    }
}
