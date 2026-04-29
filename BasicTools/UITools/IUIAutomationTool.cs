#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Tools.Contracts;

    public interface IUIAutomationTool : ITool
    {
        Task<string> FindWindowAsync(string windowName, CancellationToken ct = default);
        Task<string> NavigateAsync(string url, string? windowName = null, CancellationToken ct = default);
        Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default);
        Task<string> ClickButtonAsync(string buttonText, CancellationToken ct = default);
        Task<string> GetTextAsync(string elementName, CancellationToken ct = default);
        Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default);
        Task<string> GetTableContentAsync(string tableName, CancellationToken ct = default);
        Task<string> TakeScreenshotAsync(string filePath, CancellationToken ct = default);
        Task<string> WaitForElementAsync(string elementName, int timeoutMs = 5000, CancellationToken ct = default);
        Task<string> SelectOptionAsync(string elementName, string value, CancellationToken ct = default);
        Task<string> SendKeysAsync(string keys, CancellationToken ct = default);
        Task<string> GetAttributeAsync(string elementName, string attributeName, CancellationToken ct = default);
    }
}

#endif
