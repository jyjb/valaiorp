#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using System.Windows.Automation;
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

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var actionStr = parameters.GetString("action");
                if (!Enum.TryParse<UIAutomationAction>(actionStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown action '{actionStr}'." });

                var element = parameters.GetString("element");
                var value   = parameters.GetString("value");

                var result = action switch
                {
                    UIAutomationAction.FindWindow      => await FindWindowAsync(element, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickText       => await ClickAsync(element, null, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickButton     => await ClickAsync(element, null, ct).ConfigureAwait(false),
                    UIAutomationAction.ClickElement    => await ClickAsync(element, parameters.GetString("automationId"), ct).ConfigureAwait(false),
                    UIAutomationAction.GetText         => await GetTextAsync(element, ct).ConfigureAwait(false),
                    UIAutomationAction.SetText         => await SetTextAsync(element, value, ct).ConfigureAwait(false),
                    UIAutomationAction.GetTableContent => await GetTableContentAsync(element, ct).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported action: {actionStr}")
                };

                return ToolResult.Ok(new { Action = actionStr, Result = result });
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        protected virtual AutomationElement GetRootElement() => AutomationElement.RootElement;

        public async Task<string> FindWindowAsync(string windowName, CancellationToken ct = default)
        {
            await Task.Yield();
            var window = GetRootElement().FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, windowName));
            return window != null ? "Found" : "Not Found";
        }

        public async Task<string> ClickAsync(string elementName, string? automationId = null, CancellationToken ct = default)
        {
            await Task.Yield();
            Condition condition = !string.IsNullOrEmpty(automationId)
                ? new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, elementName),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId))
                : new PropertyCondition(AutomationElement.NameProperty, elementName);

            var element = GetRootElement().FindFirst(TreeScope.Descendants, condition);
            if (element != null)
            {
                (element.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern)?.Invoke();
                return $"Clicked: {elementName}";
            }
            return $"Element not found: {elementName}";
        }

        public async Task<string> GetTextAsync(string elementName, CancellationToken ct = default)
        {
            await Task.Yield();
            var element = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            return element?.Current.Name ?? "Not Found";
        }

        public async Task<string> SetTextAsync(string elementName, string text, CancellationToken ct = default)
        {
            await Task.Yield();
            var element = GetRootElement().FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, elementName));
            if (element != null)
            {
                var vp = element.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if (vp != null) { vp.SetValue(text); return $"Text set on: {elementName}"; }
            }
            return $"Element not found or not settable: {elementName}";
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
    }
}

#endif
