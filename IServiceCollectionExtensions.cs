using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeriRest
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddSeriRestClient(this IServiceCollection services,
               object? key,
               Func<IServiceProvider, object?, RestClientOptions> implementationFactory)
        {
            return services
                .AddKeyedSingleton< RestClientOptions>(key, implementationFactory)
                .AddKeyedScoped(key, (sp, serviceKey) =>

                    new SeriRestClient(
                        sp.GetRequiredKeyedService<RestClientOptions>(key),
                        sp.GetRequiredService<ILogger>())
                );
        }
    }
}