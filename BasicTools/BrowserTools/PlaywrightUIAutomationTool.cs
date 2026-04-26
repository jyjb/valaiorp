// Requires the Microsoft.Playwright NuGet package.
// Uncomment the PackageReference in the .csproj to enable this tool.
#if PLAYWRIGHT_ENABLED

namespace Valaiorp.BasicTools.BrowserTools
{
    using Microsoft.Playwright;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;

    public sealed class PlaywrightUIAutomationTool : ITool, IAsyncDisposable
    {
        private IPlaywright? _playwright;
        private IBrowser?    _browser;
        private IPage?       _page;
        private bool         _disposed;

        public string Id => "playwright-ui-automation";
        public string Name => "Playwright UI Automation";
        public string Description => "Browser automation via Microsoft Playwright (Chromium, Firefox, WebKit).";
        public ToolType Type => ToolType.External;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "Platform", "Cross-platform" },
            { "SupportedActions", new[] { "FindWindow", "Navigate", "ClickText", "ClickButton", "ClickElement", "GetText", "SetText", "GetTableContent" } }
        };

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            _browser    ??= await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false }).ConfigureAwait(false);
            _page       ??= await _browser.NewPageAsync().ConfigureAwait(false);
        }

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            string input,
            CancellationToken ct = default)
        {
            if (_page == null) await InitializeAsync(ct).ConfigureAwait(false);

            try
            {
                var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return ToolResult.BadRequest(new { Message = "Invalid input format. Use: action|parameter1|parameter2..." });

                var actionStr = parts[0].Trim();
                if (!Enum.TryParse<BrowserAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Invalid action: {actionStr}" });

                var result = action switch
                {
                    BrowserAction.FindWindow      => await FindWindowAsync(parts[1], ct).ConfigureAwait(false),
                    BrowserAction.Navigate        => await NavigateAsync(parts[1]).ConfigureAwait(false),
                    BrowserAction.ClickText       => await ClickAsync(parts[1], null, ct).ConfigureAwait(false),
                    BrowserAction.ClickButton     => await ClickAsync(parts[1], null, ct).ConfigureAwait(false),
                    BrowserAction.ClickElement    => await ClickAsync(parts[1], parts.Length > 2 ? parts[2] : null, ct).ConfigureAwait(false),
                    BrowserAction.GetText         => await GetTextAsync(parts[1], ct).ConfigureAwait(false),
                    BrowserAction.SetText         => await SetTextAsync(parts[1], parts[2], ct).ConfigureAwait(false),
                    BrowserAction.GetTableContent => await GetTableContentAsync(parts[1], ct).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported action: {actionStr}")
                };

                return ToolResult.Ok(new { Action = actionStr, Result = result });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        public async Task<string> FindWindowAsync(string windowName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _page!.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            var title = await _page.TitleAsync().ConfigureAwait(false);
            return title.Contains(windowName, StringComparison.OrdinalIgnoreCase) ? "Found" : "Not Found";
        }

        public async Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (automationId != null)
                    await _page!.GetByTestId(automationId).ClickAsync().ConfigureAwait(false);
                else
                    await _page!.GetByText(elementName).ClickAsync().ConfigureAwait(false);
                return $"Clicked: {elementName}";
            }
            catch { return $"Element not found: {elementName}"; }
        }

        public async Task<string> GetTextAsync(string elementName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = await _page!.GetByText(elementName).TextContentAsync().ConfigureAwait(false);
                return text ?? "Not Found";
            }
            catch { return $"Element not found: {elementName}"; }
        }

        public async Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _page!.GetByPlaceholder(elementName).FillAsync(text).ConfigureAwait(false);
                return $"Text set on: {elementName}";
            }
            catch { return $"Element not found or not settable: {elementName}"; }
        }

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

        private async Task<string> NavigateAsync(string url)
        {
            await _page!.GotoAsync(url).ConfigureAwait(false);
            return $"Navigated to: {url}";
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
        FindWindow,
        Navigate,
        ClickText,
        ClickButton,
        ClickElement,
        GetText,
        SetText,
        GetTableContent
    }
}

#endif
