namespace Valaiorp.Core.Contracts
{
    public sealed record ModuleResult
    {
        public required int Status { get; init; }
        public object? Results { get; init; }
        public object? Errors { get; init; }
        public bool IsSuccess => Status is >= 200 and < 300;
        public string? ModuleId { get; init; }
        public IReadOnlyCollection<ToolResult> StepResults { get; init; } = Array.Empty<ToolResult>();

        public static ModuleResult Ok(object? results = null, IReadOnlyCollection<ToolResult>? steps = null) =>
            new() { Status = 200, Results = results, StepResults = steps ?? Array.Empty<ToolResult>() };

        public static ModuleResult Error(string message, IReadOnlyCollection<ToolResult>? steps = null) =>
            new() { Status = 500, Errors = new { Message = message }, StepResults = steps ?? Array.Empty<ToolResult>() };

        public static ModuleResult Error(Exception ex, IReadOnlyCollection<ToolResult>? steps = null) =>
            new() { Status = 500, Errors = new { Message = ex.Message, ExceptionType = ex.GetType().Name }, StepResults = steps ?? Array.Empty<ToolResult>() };
    }
}
