#if WINDOWS

namespace Valaiorp.BasicTools.UITools
{
    using Valaiorp.Tools.Contracts;

    public static class UIAutomationToolFactory
    {
        public static ITool CreatePrimaryTool() => new WindowsUIAutomationTool();

        public static IUIAutomationTool CreatePrimaryUIAutomationTool() => new WindowsUIAutomationTool();
    }
}

#endif
