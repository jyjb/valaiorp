namespace Valaiorp.Execution.Extensions
{
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Execution.Models;

    public static class ExecutionContextAutonomyExtensions
    {
        private const string AutonomyConfigKey = "AutonomyConfig";

        public static void SetAutonomyConfig(this IExecutionContext context, AutonomyConfig config)
        {
            context.Metadata[AutonomyConfigKey] = config;
        }

        public static AutonomyContext GetAutonomyContext(this IExecutionContext context)
        {
            if (context.Metadata.TryGetValue(AutonomyConfigKey, out var configObj) && configObj is AutonomyConfig config)
            {
                return new AutonomyContext(config, context);
            }
            return new AutonomyContext(new AutonomyConfig(), context);
        }
    }
}