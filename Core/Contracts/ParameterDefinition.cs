namespace Valaiorp.Core.Contracts
{
    public sealed class ParameterDefinition
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Type { get; init; }
        public bool Required { get; init; } = true;
        public object? DefaultValue { get; init; }
    }
}
