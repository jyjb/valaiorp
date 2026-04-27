namespace Valaiorp.Tools.Contracts
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;

    public interface ITool
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        ToolType Type { get; }
        IReadOnlyDictionary<string, object> Metadata { get; }
        Task<ToolResult> ExecuteAsync(IExecutionContext context, IReadOnlyDictionary<string, object> parameters, CancellationToken ct = default);
    }
}