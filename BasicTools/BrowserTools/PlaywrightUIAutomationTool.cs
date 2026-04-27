// Requires the Microsoft.Playwright NuGet package.
// Uncomment the PackageReference in the .csproj to enable this tool.
#if PLAYWRIGHT_ENABLED

namespace Valaiorp.BasicTools.BrowserTools
{
    using Microsoft.Playwright;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Helpers;

    public sealed class PlaywrightUIAutomationTool : ITool, IAsyncDisposable
    {
        private IPlaywright? _playwright;
        private IBrowser?    _browser;
        private IPage?       _page;
        private bool         _disposed;

        public string Id => "playwright-ui-automation";
        public string Name => "Playwright UI Automation";
        public string Description => "Browser automation via Microsoft Playwright. Parameters: action (FindWindow|Navigate|ClickText|ClickButton|ClickElement|GetText|SetText|GetTableContent), element, value (SetText only), url (Navigate only).";
        public ToolType Type => ToolType.External;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["Platform"] = "Cross-platform",
            ["SupportedActions"] = new[] { "FindWindow", "Navigate", "ClickText", "ClickButton", "ClickElement", "GetText", "SetText", "GetTableContent" }
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
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            if (_page == null) await InitializeAsync(ct).ConfigureAwait(false);
            try
            {
                var actionStr = parameters.GetString("action");
                if (!Enum.TryParse<BrowserAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown action '{actionStr}'." });

                var element = parameters.GetString("element");
                var value   = parameters.GetString("value");
                var url     = parameters.GetString("url");

                var result = action switch
                {
                    BrowserAction.FindWindow      => await FindWindowAsync(element, ct).ConfigureAwait(false),
                    BrowserAction.Navigate        => await NavigateAsync(url).ConfigureAwait(false),
                    BrowserAction.ClickText       => await ClickAsync(element, null, ct).ConfigureAwait(false),
                    BrowserAction.ClickButton     => await ClickAsync(element, null, ct).ConfigureAwait(false),
                    BrowserAction.ClickElement    => await ClickAsync(element, parameters.GetString("automationId"), ct).ConfigureAwait(false),
                    BrowserAction.GetText         => await GetTextAsync(element, ct).ConfigureAwait(false),
                    BrowserAction.SetText         => await SetTextAsync(element, value, ct).ConfigureAwait(false),
                    BrowserAction.GetTableContent => await GetTableContentAsync(element, ct).ConfigureAwait(false),
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
                if (!string.IsNullOrWhiteSpace(automationId))
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
            try { return await _page!.GetByText(elementName).TextContentAsync().ConfigureAwait(false) ?? "Not Found"; }
            catch { return $"Element not found: {elementName}"; }
        }

        public async Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try { await _page!.GetByPlaceholder(elementName).FillAsync(text).ConfigureAwait(false); return $"Text set on: {elementName}"; }
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
        FindWindow, Navigate, ClickText, ClickButton, ClickElement, GetText, SetText, GetTableContent
    }
}

#endif
