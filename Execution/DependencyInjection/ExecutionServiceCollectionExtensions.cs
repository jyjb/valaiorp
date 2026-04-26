namespace Valaiorp.Execution.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Execution.Executors;
    using Valaiorp.Execution.Workflow;
    using Valaiorp.Tools.Resolvers;

    public static class ExecutionServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowSupport(this IServiceCollection services)
        {
            services.AddSingleton<WorkflowExecutor>(provider => new WorkflowExecutor(
                provider.GetRequiredService<ToolResolver>(),
                provider.GetRequiredService<ParallelExecutor>()));
            return services;
        }
    }
}
