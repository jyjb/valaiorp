namespace Valaiorp.Retry.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Retry.Contracts;
    using Valaiorp.Retry.Policies;
    using Valaiorp.Retry.Strategies;

    public static class RetryServiceCollectionExtensions
    {
        public static IServiceCollection AddRetryModule(this IServiceCollection services)
        {
            services.AddSingleton(_ => new MaxAttemptsRetryPolicy(3));
            services.AddSingleton(_ => new CircuitBreakerRetryPolicy(5, TimeSpan.FromSeconds(30)));
            services.AddSingleton(_ => new ExponentialBackoffRetryPolicy(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10)));

            services.AddSingleton<IRetryStrategy>(provider =>
            {
                var compositePolicy = new CompositeRetryPolicy(
                    provider.GetRequiredService<MaxAttemptsRetryPolicy>(),
                    provider.GetRequiredService<CircuitBreakerRetryPolicy>(),
                    provider.GetRequiredService<ExponentialBackoffRetryPolicy>());

                return new RetryStrategy(compositePolicy);
            });

            return services;
        }
    }
}