#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Tools.Contracts;

    public interface IUIAutomationTool : ITool
    {
        Task<string> FindWindowAsync(string windowName, CancellationToken ct = default);
        Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default);
        Task<string> GetTextAsync(string elementName, CancellationToken ct = default);
        Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default);
        Task<string> GetTableContentAsync(string tableName, CancellationToken ct = default);
    }
}

#endif
