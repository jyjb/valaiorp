#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    using System.Windows.Automation;
    using Valaiorp.BasicTools;
    using Valaiorp.BasicTools.FileTools;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Helpers;

    public abstract class UIAutomationBaseTool : IUIAutomationTool
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract ToolType Type { get; }
        public abstract IReadOnlyDictionary<string, object> Metadata { get; }

        private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
            { "msedge", "chrome", "firefox", "brave", "opera" };

        // Known address bar names across Edge, Chrome, Firefox
        private static readonly string[] AddressBarNames =
        [
            "Address and search bar",
            "Address and Search Bar",
            "Address bar",
            "search or enter address",
        ];

        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
        [DllImport("user32.dll")] private static extern short VkKeyScan(char ch);

        private static void PressVk(byte vk)
        {
            keybd_event(vk, 0, 0, IntPtr.Zero);
            keybd_event(vk, 0, 2, IntPtr.Zero);
        }

        // ── Pipe-delimited dispatch ──────────────────────────────────────────
        // Protocol: input = "Action|param1|param2"
        // Navigate|url[|windowName]   ClickElement|name[|automationId]   SetText|name|value
        // WaitForElement|name[|timeoutMs]   GetAttribute|name|attribute   Screenshot[|filePath]
        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                // Accept pipe-delimited "input" OR separate "action"/"element"/"url"/"value" params
                string[] parts;
                var raw = parameters.GetString("input");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    parts = raw.Split('|', 3);
                }
                else
                {
                    var action2 = parameters.GetString("action");
                    if (string.IsNullOrWhiteSpace(action2))
                        return ToolResult.BadRequest(new { Message = "Parameter 'input' is required. Format: Action|param1|param2" });
                    var p1b = parameters.GetString("element", parameters.GetString("url"));
                    var p2b = parameters.GetString("value", parameters.GetString("automationId"));
                    parts = string.IsNullOrEmpty(p2b)
                        ? [action2, p1b]
                        : [action2, p1b, p2b];
                }

                var actionStr = parts[0];
                var p1 = parts.Length > 1 ? parts[1] : "";
                var p2 = parts.Length > 2 ? parts[2] : "";

                if (!Enum.TryParse<UIAutomationAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown action '{actionStr}'. Supported: {string.Join(", ", Enum.GetNames(typeof(UIAutomationAction)))}" });

                int.TryParse(p2, out var timeoutMs);

                // Log security-sensitive desktop actions before execution
                switch (action)
                {
                    case UIAutomationAction.Navigate:
                        ToolSecurityLog.Write(Id, "Navigate", context.Id, new { url = p1, window = p2 });
                        break;
                    case UIAutomationAction.SetText:
                        ToolSecurityLog.Write(Id, "SetText", context.Id, new { element = p1 });
                        break;
                    case UIAutomationAction.SendKeys:
                        ToolSecurityLog.Write(Id, "SendKeys", context.Id, new { keys = p1 });
                        break;
                    case UIAutomationAction.Screenshot:
                        if (!string.IsNullOrWhiteSpace(p1)) p1 = PathGuard.Validate(p1);
                        ToolSecurityLog.Write(Id, "Screenshot", context.Id, new { filePath = p1 });
                        break;
                }

                var result = action switch
                {
                    UIAutomationAction.FindWindow      => await FindWindowAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.Navigate        => await NavigateAsync(p1, string.IsNullOrEmpty(p2) ? null : p2, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickText       => await ClickAsync(p1, null, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickButton     => await ClickButtonAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickElement    => await ClickAsync(p1, string.IsNullOrEmpty(p2) ? null : p2, ct).ConfigureAwait(false),
                    UIAutomationAction.GetText         => await GetTextAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.SetText         => await SetTextAsync(p1, p2, ct).ConfigureAwait(false),
                    UIAutomationAction.GetTableContent => await GetTableContentAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.Screenshot      => await TakeScreenshotAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.WaitForElement  => await WaitForElementAsync(p1, timeoutMs > 0 ? timeoutMs : 5000, ct).ConfigureAwait(false),
                    UIAutomationAction.SelectOption    => await SelectOptionAsync(p1, p2, ct).ConfigureAwait(false),
                    UIAutomationAction.SendKeys        => await SendKeysAsync(p1, ct).ConfigureAwait(false),
                    UIAutomationAction.GetAttribute    => await GetAttributeAsync(p1, p2, ct).ConfigureAwait(false),
                    UIAutomationAction.GetPageText     => await GetPageTextAsync(ct).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported action: {actionStr}")
                };

                return ToolResult.Ok(new { Action = actionStr, Result = result });
            }
            catch (InvalidOperationException ex)
            {
                // UIA errors (unsupported pattern, element gone stale, etc.) are recoverable —
                // return as observable text so the agent can adapt rather than aborting the run.
                return ToolResult.Ok(new { Action = "Error", Result = ex.Message });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        protected virtual AutomationElement GetRootElement() => AutomationElement.RootElement;

        // ── Core actions ─────────────────────────────────────────────────────

        public async Task<string> FindWindowAsync(string windowName, CancellationToken ct = default)
        {
            await Task.Yield();
            var win = GetRootElement().FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, windowName,
                    PropertyConditionFlags.IgnoreCase));
            return win != null ? "Found" : "Not Found";
        }

        public async Task<string> NavigateAsync(string url, string? windowName = null, CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var searchRoot = GetRootElement();
            if (!string.IsNullOrWhiteSpace(windowName))
            {
                var win = searchRoot.FindFirst(TreeScope.Children,
                    new PropertyCondition(AutomationElement.NameProperty, windowName,
                        PropertyConditionFlags.IgnoreCase));
                if (win == null) return $"Window not found: {windowName}";
                searchRoot = win;
            }

            AutomationElement? addressBar = null;
            foreach (var barName in AddressBarNames)
            {
                addressBar = searchRoot.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.NameProperty, barName,
                            PropertyConditionFlags.IgnoreCase)));
                if (addressBar != null) break;
            }

            if (addressBar == null)
            {
                // No browser open — launch Edge and retry
                try
                {
                    Process.Start(new ProcessStartInfo("msedge.exe", "--new-window") { UseShellExecute = true });
                    await Task.Delay(3000, ct).ConfigureAwait(false);
                }
                catch
                {
                    return "No browser address bar found and could not launch Edge. Ensure Edge, Chrome, or Firefox is open.";
                }
                foreach (var barName in AddressBarNames)
                {
                    addressBar = searchRoot.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                            new PropertyCondition(AutomationElement.NameProperty, barName,
                                PropertyConditionFlags.IgnoreCase)));
                    if (addressBar != null) break;
                }
                if (addressBar == null)
                    return "Launched Edge but address bar still not found after 3 s. Try again.";
            }

            addressBar.SetFocus();
            if (addressBar.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern vp)
                return "Address bar does not support ValuePattern.";

            vp.SetValue(url);
            PressVk(0x0D); // VK_RETURN
            return $"Navigated to: {url}";
        }

        public async Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default)
        {
            await Task.Yield();
            Condition condition = !string.IsNullOrEmpty(automationId)
                ? new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, elementName),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId))
                : new PropertyCondition(AutomationElement.NameProperty, elementName);

            var el = GetRootElement().FindFirst(TreeScope.Descendants, condition);
            if (el == null) return $"Element not found: {elementName}";
            (el.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern)?.Invoke();
            return $"Clicked: {elementName}";
        }

        public async Task<string> ClickButtonAsync(string buttonText, CancellationToken ct = default)
        {
            await Task.Yield();
            var el = GetRootElement().FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, buttonText,
                        PropertyConditionFlags.IgnoreCase),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
            if (el == null) return $"Button not found: {buttonText}";
            (el.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern)?.Invoke();
            return $"Button clicked: {buttonText}";
        }

        public async Task<string> GetTextAsync(string elementName, CancellationToken ct = default)
        {
            await Task.Yield();
            var el = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            if (el == null) return "Not Found";
            try
            {
                if (el.GetCurrentPattern(ValuePattern.Pattern) is ValuePattern vp)
                    return vp.Current.Value;
                return el.Current.Name;
            }
            catch (InvalidOperationException)
            {
                return "Not Found";
            }
        }

        public async Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default)
        {
            await Task.Yield();
            var el = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            if (el == null) return $"Element not found: {elementName}";
            if (el.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern vp)
                return $"Element not settable: {elementName}";
            vp.SetValue(text);
            return $"Text set on: {elementName}";
        }

        public async Task<string> GetTableContentAsync(string tableName, CancellationToken ct = default)
        {
            await Task.Yield();
            var table = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, tableName));
            if (table == null) return $"Table not found: {tableName}";

            var rows = table.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));

            var tableData = new List<List<string>>();
            foreach (AutomationElement row in rows)
            {
                var cells = row.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                var rowData = new List<string>();
                foreach (AutomationElement cell in cells) rowData.Add(cell.Current.Name);
                tableData.Add(rowData);
            }
            return System.Text.Json.JsonSerializer.Serialize(tableData);
        }

        public async Task<string> TakeScreenshotAsync(string filePath, CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            var path = string.IsNullOrWhiteSpace(filePath)
                ? PathGuard.Validate($"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png")
                : PathGuard.Validate(filePath);
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bmp.Save(path, ImageFormat.Png);
            return $"Screenshot saved: {path}";
        }

        public async Task<string> WaitForElementAsync(string elementName, int timeoutMs = 5000, CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                var el = GetRootElement().FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, elementName));
                if (el != null) return $"Element appeared: {elementName}";
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            return $"Element did not appear within {timeoutMs}ms: {elementName}";
        }

        public async Task<string> SelectOptionAsync(string elementName, string value, CancellationToken ct = default)
        {
            await Task.Yield();
            var container = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            if (container == null) return $"Element not found: {elementName}";

            // Expand ComboBox if needed
            if (container.GetCurrentPattern(ExpandCollapsePattern.Pattern) is ExpandCollapsePattern ecp)
                ecp.Expand();

            var item = container.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, value,
                    PropertyConditionFlags.IgnoreCase));
            if (item == null) return $"Option '{value}' not found in: {elementName}";

            (item.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern)?.Select();
            return $"Selected '{value}' on: {elementName}";
        }

        public async Task<string> SendKeysAsync(string keys, CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            // Named special keys
            byte specialVk = keys.ToUpperInvariant() switch
            {
                "ENTER"    or "{ENTER}"    => 0x0D,
                "TAB"      or "{TAB}"      => 0x09,
                "ESC"      or "{ESC}"      => 0x1B,
                "BACKSPACE"               => 0x08,
                "DELETE"                  => 0x2E,
                "HOME"                    => 0x24,
                "END"                     => 0x23,
                "PAGEUP"                  => 0x21,
                "PAGEDOWN"               => 0x22,
                "UP"                      => 0x26,
                "DOWN"                    => 0x28,
                "LEFT"                    => 0x25,
                "RIGHT"                   => 0x27,
                "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
                "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
                "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                _ => 0
            };

            if (specialVk != 0)
            {
                PressVk(specialVk);
                return $"Key sent: {keys}";
            }

            // Send as individual characters
            foreach (var ch in keys)
            {
                ct.ThrowIfCancellationRequested();
                var vk = VkKeyScan(ch);
                var shifted = (vk & 0x0100) != 0;
                if (shifted) keybd_event(0x10, 0, 0, IntPtr.Zero);  // Shift down
                PressVk((byte)(vk & 0xFF));
                if (shifted) keybd_event(0x10, 0, 2, IntPtr.Zero);  // Shift up
            }
            return $"Keys sent: {keys}";
        }

        public async Task<string> GetAttributeAsync(string elementName, string attributeName, CancellationToken ct = default)
        {
            await Task.Yield();
            var el = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            if (el == null) return $"Element not found: {elementName}";

            return attributeName.ToLowerInvariant() switch
            {
                "name"         => el.Current.Name,
                "automationid" => el.Current.AutomationId,
                "controltype"  => el.Current.ControlType.LocalizedControlType,
                "isenabled"    => el.Current.IsEnabled.ToString(),
                "isvisible"    => (!el.Current.IsOffscreen).ToString(),
                "helptext"     => el.Current.HelpText,
                "value"        => (el.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern)?.Current.Value ?? "N/A",
                "classname"    => el.Current.ClassName,
                "processid"    => el.Current.ProcessId.ToString(),
                _ => $"Unknown attribute '{attributeName}'. Supported: name, automationid, controltype, isenabled, isvisible, helptext, value, classname, processid"
            };
        }

        // Returns all visible text from the active browser page via TextPattern on the Document element.
        // Searches specifically within browser process windows to avoid picking up VS Code or other editors.
        // Capped at 8000 chars to keep LLM context manageable.
        public async Task<string> GetPageTextAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            var root = GetRootElement();

            var topWindows = root.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

            foreach (AutomationElement win in topWindows)
            {
                try
                {
                    var procName = Process.GetProcessById(win.Current.ProcessId).ProcessName;
                    if (!BrowserProcessNames.Contains(procName)) continue;
                }
                catch { continue; }

                var doc = win.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
                if (doc == null) continue;

                try
                {
                    if (doc.GetCurrentPattern(TextPattern.Pattern) is not TextPattern tp) continue;
                    var text = tp.DocumentRange.GetText(8000);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                catch (InvalidOperationException) { }
            }

            return "No browser page text found. Ensure Edge, Chrome, or Firefox is open with a loaded page.";
        }
    }
}

#endif
