namespace Valaiorp.Tools.Contracts
{
    using Valaiorp.Core.Contracts;

    public interface IModule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IReadOnlyDictionary<string, ParameterDefinition> Parameters { get; }
        IReadOnlyCollection<ITool> Tools { get; }
        Task InitializeAsync(CancellationToken ct = default);
        Task<ModuleResult> ExecuteAsync(IExecutionContext context, IReadOnlyDictionary<string, object> parameters, CancellationToken ct = default);
    }
}