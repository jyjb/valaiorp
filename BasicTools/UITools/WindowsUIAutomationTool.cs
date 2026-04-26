#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Core.Enums;

    public sealed class WindowsUIAutomationTool : UIAutomationBaseTool
    {
        public override string Id => "windows-ui-automation";
        public override string Name => "Windows UI Automation";
        public override string Description => "Desktop UI automation for Windows applications (Win32, WPF, WinForms) and browsers via UIA.";
        public override ToolType Type => ToolType.Native;
        public override IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "Platform", "Windows" },
            { "SupportedActions", Enum.GetNames(typeof(UIAutomationAction)) }
        };
    }
}

#endif
