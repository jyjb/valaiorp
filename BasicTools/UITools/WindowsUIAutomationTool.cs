#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Core.Enums;

    public sealed class WindowsUIAutomationTool : UIAutomationBaseTool
    {
        public override string Id => "windows-ui-automation";
        public override string Name => "Windows UI Automation";
        public override string Description => "Desktop UI automation for Windows apps and browsers (Edge, Chrome, Firefox) via UIA. Parameter: input (pipe-delimited string). Format: Action|param1[|param2]. Actions: Navigate|<url>, FindWindow|<title>, GetText|<elementName>, SetText|<elementName>|<value>, ClickText|<name>, ClickButton|<name>, WaitForElement|<name>[|<ms>], Screenshot[|<filePath>], GetTableContent|<name>, GetPageText (no params — returns all visible text from the active browser page).";
        public override ToolType Type => ToolType.Native;
        public override IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "Platform", "Windows" },
            { "SupportedActions", Enum.GetNames(typeof(UIAutomationAction)) }
        };
    }
}

#endif
