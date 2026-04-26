namespace Valaiorp.Runtime.Bootstrap
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Configuration.Config;
    using Valaiorp.Runtime.Configuration;
    using Valaiorp.Runtime.DependencyInjection;
    using Valaiorp.BasicTools.Registries;
    using Valaiorp.Tools.Registries;

    public static class RuntimeBuilder
    {
        public static AgentRuntime Build(AgenticAIConfig? config = null, Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();

            var effectiveConfig = config ?? RuntimeConfigLoader.LoadDefault();
            services.AddAgenticAIRuntime(effectiveConfig);

            configureServices?.Invoke(services);

            var provider = services.BuildServiceProvider();
            BasicToolsRegistry.RegisterAll(provider.GetRequiredService<ToolRegistry>());
            return provider.GetRequiredService<AgentRuntime>();
        }

        public static AgentRuntime BuildFromFile(string configFilePath, Action<IServiceCollection>? configureServices = null)
        {
            var config = RuntimeConfigLoader.LoadFromFile(configFilePath);
            return Build(config, configureServices);
        }
    }
}
