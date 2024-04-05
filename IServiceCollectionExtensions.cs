using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using ILogger = Serilog.ILogger;

namespace SeriRest
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddSeriRestClient(this IServiceCollection services,
               object? key,
               Func<IServiceProvider, object?, RestClientOptions> implementationFactory)
        {
            return services
                .AddKeyedSingleton<RestClientOptions>(key, implementationFactory)
                .AddKeyedScoped(key, (sp, serviceKey) =>

                    new SeriRestClient(
                        sp.GetRequiredKeyedService<RestClientOptions>(key),
                        sp.GetRequiredService<ILogger>())
                );
        }
    }
}