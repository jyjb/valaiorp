namespace Valaiorp.Tools.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IModule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IReadOnlyCollection<ITool> Tools { get; }
        Task InitializeAsync(CancellationToken ct = default);
    }
}