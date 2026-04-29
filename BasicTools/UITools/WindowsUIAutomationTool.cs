#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Core.Enums;

    public sealed class WindowsUIAutomationTool : UIAutomationBaseTool
    {
        public override string Id => "windows-ui-automation";
        public override string Name => "Windows UI Automation";
        public override string Description => "Desktop UI automation for Windows applications (Win32, WPF, WinForms) and browsers (Edge, Chrome, Firefox) via UIA. Parameters: action (FindWindow|Navigate|ClickText|ClickButton|ClickElement|GetText|SetText|GetTableContent), element (element/window name), url (Navigate only), automationId (ClickElement only), value (SetText only).";
        public override ToolType Type => ToolType.Native;
        public override IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "Platform", "Windows" },
            { "SupportedActions", Enum.GetNames(typeof(UIAutomationAction)) }
        };
    }
}

#endif
