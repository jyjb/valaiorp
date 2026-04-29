// Requires the Microsoft.Playwright NuGet package.
// Uncomment the PackageReference in the .csproj to enable this tool.
#if PLAYWRIGHT_ENABLED

namespace Valaiorp.BasicTools.BrowserTools
{
    using Microsoft.Playwright;
    using Valaiorp.BasicTools;
    using Valaiorp.BasicTools.FileTools;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public sealed class PlaywrightUIAutomationTool : IBrowserAutomationTool
    {
        private IPlaywright? _playwright;
        private IBrowser?    _browser;
        private IPage?       _page;
        private bool         _disposed;

        public string Id => "playwright-ui-automation";
        public string Name => "Playwright UI Automation";
        public string Description => """
            Browser automation via Microsoft Playwright.
            Input format (pipe-delimited): Action|param1|param2
            | Action          | Format                          | Example                              |
            | Navigate        | Navigate|url                    | Navigate|https://example.com         |
            | ClickText       | ClickText|text                  | ClickText|Submit                     |
            | ClickButton     | ClickButton|buttonText           | ClickButton|Login                    |
            | ClickElement    | ClickElement|cssSelector         | ClickElement|#submit-button          |
            | GetText         | GetText|cssSelector              | GetText|.result                      |
            | SetText         | SetText|cssSelector|value        | SetText|#username|admin              |
            | GetTableContent | GetTableContent|cssSelector      | GetTableContent|table.data           |
            | Screenshot      | Screenshot[|filePath]            | Screenshot|output.png                |
            | WaitForElement  | WaitForElement|text[|timeoutMs]  | WaitForElement|Loading...|3000       |
            | SelectOption    | SelectOption|label|value         | SelectOption|Country|Canada          |
            | SendKeys        | SendKeys|keys                    | SendKeys|Enter                       |
            | GetAttribute    | GetAttribute|cssSelector|attr    | GetAttribute|#link|href              |
            | EvaluateJs      | EvaluateJs|script               | EvaluateJs|document.title            |
            | FindWindow      | FindWindow|pageTitle             | FindWindow|GitHub                    |
            """;
        public ToolType Type => ToolType.External;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["Platform"] = "Cross-platform",
            ["SupportedActions"] = Enum.GetNames(typeof(BrowserAction))
        };

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            _browser    ??= await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false }).ConfigureAwait(false);
            _page       ??= await _browser.NewPageAsync().ConfigureAwait(false);
        }

        // ── Pipe-delimited dispatch ──────────────────────────────────────────
        // Protocol: input = "Action|param1|param2"
        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            if (_page == null) await InitializeAsync(ct).ConfigureAwait(false);
            try
            {
                var parts = parameters.ParsePipeInput();
                if (parts.Length == 0)
                    return ToolResult.BadRequest(new { Message = "Parameter 'input' is required. Format: Action|param1|param2" });

                var actionStr = parts[0];
                var p1 = parts.Length > 1 ? parts[1] : "";
                var p2 = parts.Length > 2 ? parts[2] : "";

                if (!Enum.TryParse<BrowserAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown action '{actionStr}'. Supported: {string.Join(", ", Enum.GetNames(typeof(BrowserAction)))}" });

                int.TryParse(p2, out var timeoutMs);

                // Log security-sensitive actions before execution
                switch (action)
                {
                    case BrowserAction.Navigate:
                        ToolSecurityLog.Write(Id, "Navigate", context.Id, new { url = p1 });
                        break;
                    case BrowserAction.SendKeys:
                        ToolSecurityLog.Write(Id, "SendKeys", context.Id, new { keys = p1 });
                        break;
                    case BrowserAction.EvaluateJs:
                        ToolSecurityLog.Write(Id, "EvaluateJs", context.Id,
                            new { script = p1.Length > 200 ? p1[..200] + "…" : p1 });
                        break;
                    case BrowserAction.Screenshot:
                        if (!string.IsNullOrWhiteSpace(p1)) p1 = PathGuard.Validate(p1);
                        ToolSecurityLog.Write(Id, "Screenshot", context.Id, new { filePath = p1 });
                        break;
                }

                var result = action switch
                {
                    BrowserAction.FindWindow      => await FindWindowAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.Navigate        => await NavigateAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.ClickText       => await ClickAsync(p1, null, ct).ConfigureAwait(false),
                    BrowserAction.ClickButton     => await ClickButtonAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.ClickElement    => await ClickByLocatorAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.GetText         => await GetTextByLocatorAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.SetText         => await SetTextByLocatorAsync(p1, p2, ct).ConfigureAwait(false),
                    BrowserAction.GetTableContent => await GetTableContentByLocatorAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.Screenshot      => await TakeScreenshotAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.WaitForElement  => await WaitForElementAsync(p1, timeoutMs > 0 ? timeoutMs : 5000, ct).ConfigureAwait(false),
                    BrowserAction.SelectOption    => await SelectOptionAsync(p1, p2, ct).ConfigureAwait(false),
                    BrowserAction.SendKeys        => await SendKeysAsync(p1, ct).ConfigureAwait(false),
                    BrowserAction.GetAttribute    => await GetAttributeAsync(p1, p2, ct).ConfigureAwait(false),
                    BrowserAction.EvaluateJs      => await EvaluateJsAsync(p1, ct).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported action: {actionStr}")
                };

                return ToolResult.Ok(new { Action = actionStr, Result = result });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        // ── Core actions ─────────────────────────────────────────────────────

        public async Task<string> FindWindowAsync(string windowName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _page!.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            var title = await _page.TitleAsync().ConfigureAwait(false);
            return title.Contains(windowName, StringComparison.OrdinalIgnoreCase) ? "Found" : "Not Found";
        }

        public async Task<string> NavigateAsync(string url, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _page!.GotoAsync(url).ConfigureAwait(false);
            return $"Navigated to: {url}";
        }

        // ClickText: semantic text match
        public async Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!string.IsNullOrWhiteSpace(automationId))
                    await _page!.GetByTestId(automationId).ClickAsync().ConfigureAwait(false);
                else
                    await _page!.GetByText(elementName).ClickAsync().ConfigureAwait(false);
                return $"Clicked: {elementName}";
            }
            catch { return $"Element not found: {elementName}"; }
        }

        // ClickButton: ARIA button role match
        public async Task<string> ClickButtonAsync(string buttonText, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _page!.GetByRole(AriaRole.Button, new() { Name = buttonText }).ClickAsync().ConfigureAwait(false);
                return $"Button clicked: {buttonText}";
            }
            catch { return $"Button not found: {buttonText}"; }
        }

        // ClickElement: CSS/XPath selector
        private async Task<string> ClickByLocatorAsync(string selector, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { await _page!.Locator(selector).ClickAsync().ConfigureAwait(false); return $"Clicked: {selector}"; }
            catch { return $"Selector not found: {selector}"; }
        }

        // GetText: CSS/XPath selector → innerText
        public async Task<string> GetTextAsync(string elementName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { return await _page!.GetByText(elementName).TextContentAsync().ConfigureAwait(false) ?? "Not Found"; }
            catch { return $"Element not found: {elementName}"; }
        }

        private async Task<string> GetTextByLocatorAsync(string selector, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { return await _page!.Locator(selector).TextContentAsync().ConfigureAwait(false) ?? "Not Found"; }
            catch { return $"Selector not found: {selector}"; }
        }

        // SetText: CSS/XPath selector → fill
        public async Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { await _page!.GetByPlaceholder(elementName).FillAsync(text).ConfigureAwait(false); return $"Text set on: {elementName}"; }
            catch { return $"Element not found or not settable: {elementName}"; }
        }

        private async Task<string> SetTextByLocatorAsync(string selector, string text, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { await _page!.Locator(selector).FillAsync(text).ConfigureAwait(false); return $"Text set on: {selector}"; }
            catch { return $"Selector not found or not settable: {selector}"; }
        }

        // GetTableContent: CSS selector → rows × cells JSON
        public async Task<string> GetTableContentAsync(string tableName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var table = _page!.GetByRole(AriaRole.Table, new() { Name = tableName });
                var rows  = await table.GetByRole(AriaRole.Row).AllAsync().ConfigureAwait(false);
                var data  = new List<List<string>>();
                foreach (var row in rows)
                {
                    var cells = await row.GetByRole(AriaRole.Cell).AllTextContentsAsync().ConfigureAwait(false);
                    data.Add(cells.ToList());
                }
                return System.Text.Json.JsonSerializer.Serialize(data);
            }
            catch { return $"Table not found: {tableName}"; }
        }

        private async Task<string> GetTableContentByLocatorAsync(string selector, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var rows = await _page!.Locator($"{selector} tr").AllAsync().ConfigureAwait(false);
                var data = new List<List<string>>();
                foreach (var row in rows)
                {
                    var cells = await row.Locator("td, th").AllTextContentsAsync().ConfigureAwait(false);
                    data.Add(cells.ToList());
                }
                return System.Text.Json.JsonSerializer.Serialize(data);
            }
            catch { return $"Table not found: {selector}"; }
        }

        public async Task<string> TakeScreenshotAsync(string filePath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var path = string.IsNullOrWhiteSpace(filePath)
                    ? $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png"
                    : filePath;
                await _page!.ScreenshotAsync(new() { Path = path, FullPage = true }).ConfigureAwait(false);
                return $"Screenshot saved: {path}";
            }
            catch (Exception ex) { return $"Screenshot failed: {ex.Message}"; }
        }

        public async Task<string> WaitForElementAsync(string elementName, int timeoutMs = 5000, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _page!.GetByText(elementName)
                    .WaitForAsync(new() { Timeout = timeoutMs })
                    .ConfigureAwait(false);
                return $"Element appeared: {elementName}";
            }
            catch { return $"Element did not appear within {timeoutMs}ms: {elementName}"; }
        }

        public async Task<string> SelectOptionAsync(string elementName, string value, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _page!.GetByLabel(elementName).SelectOptionAsync(value).ConfigureAwait(false);
                return $"Selected '{value}' on: {elementName}";
            }
            catch { return $"Select element not found or value not available: {elementName}"; }
        }

        public async Task<string> SendKeysAsync(string keys, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _page!.Keyboard.PressAsync(keys).ConfigureAwait(false);
                return $"Keys sent: {keys}";
            }
            catch (Exception ex) { return $"SendKeys failed: {ex.Message}"; }
        }

        public async Task<string> GetAttributeAsync(string elementName, string attributeName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // elementName is treated as a CSS selector for GetAttribute
                var attr = await _page!.Locator(elementName)
                    .GetAttributeAsync(attributeName)
                    .ConfigureAwait(false);
                return attr ?? "Attribute not found";
            }
            catch { return $"Selector not found: {elementName}"; }
        }

        public async Task<string> EvaluateJsAsync(string script, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await _page!.EvaluateAsync<string>(script).ConfigureAwait(false);
                return result ?? "null";
            }
            catch (Exception ex) { return $"Script error: {ex.Message}"; }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_browser != null) await _browser.CloseAsync().ConfigureAwait(false);
                _playwright?.Dispose();
                _disposed = true;
            }
        }
    }

    public enum BrowserAction
    {
        FindWindow, Navigate,
        ClickText, ClickButton, ClickElement,
        GetText, SetText,
        GetTableContent,
        Screenshot, WaitForElement, SelectOption, SendKeys, GetAttribute, EvaluateJs
    }
}

#endif
