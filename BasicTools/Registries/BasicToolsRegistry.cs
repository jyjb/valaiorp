namespace Valaiorp.BasicTools.Registries
{
    using Valaiorp.BasicTools.ApiTools;
    using Valaiorp.BasicTools.FileTools;
    using Valaiorp.BasicTools.FolderTools;
    using Valaiorp.Tools.Registries;
#if WINDOWS
    using Valaiorp.BasicTools.UITools;
#endif

    public static class BasicToolsRegistry
    {
        public static void RegisterAll(ToolRegistry registry)
        {
            // File Tools
            registry.Register(new JsonTool());
            registry.Register(new JsonlTool());
            registry.Register(new JsoncTool());
            registry.Register(new TxtTool());
            registry.Register(new CsvTool());
            registry.Register(new TsvTool());
            registry.Register(new PsvTool());
            registry.Register(new XmlTool());
            registry.Register(new ExcelTool());
            registry.Register(new WordTool());

            // Folder Tools
            registry.Register(new FolderTool());

            // API Tools
            registry.Register(new ApiTool());

#if WINDOWS
            // UI Automation Tools (Windows only — desktop and browser via UIA)
            registry.Register(UIAutomationToolFactory.CreatePrimaryTool());
#endif

#if PLAYWRIGHT_ENABLED
            // Browser Tools (Playwright — uncomment Microsoft.Playwright package in .csproj to enable)
            registry.Register(new Valaiorp.BasicTools.BrowserTools.PlaywrightUIAutomationTool());
#endif
        }
    }
}
