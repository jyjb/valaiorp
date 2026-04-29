// Requires the Microsoft.Playwright NuGet package.
#if PLAYWRIGHT_ENABLED

namespace Valaiorp.BasicTools.BrowserTools
{
    using Valaiorp.Tools.Contracts;

    public interface IBrowserAutomationTool : ITool, IAsyncDisposable
    {
        Task InitializeAsync(CancellationToken ct = default);
        Task<string> FindWindowAsync(string windowName, CancellationToken ct = default);
        Task<string> NavigateAsync(string url, CancellationToken ct = default);
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
        Task<string> EvaluateJsAsync(string script, CancellationToken ct = default);
    }
}

#endif
