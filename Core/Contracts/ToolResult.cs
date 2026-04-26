namespace Valaiorp.Core.Contracts
{
    public sealed record ToolResult
    {
        public required int Status { get; init; }
        public object? Results { get; init; }
        public object? Errors { get; init; }
        public bool IsSuccess => Status is >= 200 and < 300;
        public string? StepId { get; init; }

        public static ToolResult Ok(object? results = null) =>
            new() { Status = 200, Results = results };

        public static ToolResult Created(object? results = null) =>
            new() { Status = 201, Results = results };

        public static ToolResult BadRequest(object? errors = null) =>
            new() { Status = 400, Errors = errors };

        public static ToolResult NotFound(string resource) =>
            new() { Status = 404, Errors = new { Message = $"Not found: {resource}" } };

        public static ToolResult Error(string message) =>
            new() { Status = 500, Errors = new { Message = message } };

        public static ToolResult Error(Exception ex) =>
            new() { Status = 500, Errors = new { Message = ex.Message, ExceptionType = ex.GetType().Name } };
    }
}
