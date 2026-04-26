namespace Valaiorp.Knowledge.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IKnowledgeProvider
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IReadOnlyDictionary<string, object> Metadata { get; }

        Task<bool> IsAvailableAsync(CancellationToken ct = default);
        Task<IReadOnlyCollection<string>> SearchAsync(
            string query,
            int maxResults,
            CancellationToken ct = default);
        Task IngestAsync(
            IEnumerable<string> documents,
            CancellationToken ct = default);
    }
}