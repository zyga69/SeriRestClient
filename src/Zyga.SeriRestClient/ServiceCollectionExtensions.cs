using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using ILogger = Serilog.ILogger;

namespace Zyga.SeriRestClient;

/// <summary>
/// Provides extension methods for registering LoggingRestClient in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILoggingRestClient"/> and <see cref="LoggingRestClient"/> as keyed services
    /// in the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="key">The service key used to identify this specific REST client instance.
    /// This allows multiple REST clients to be registered with different configurations.</param>
    /// <param name="implementationFactory">A factory function that creates <see cref="RestClientOptions"/>
    /// for configuring the REST client. Receives the service provider and key as parameters.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    /// <remarks>
    /// This method registers the REST client options as a keyed singleton and the client itself as a keyed scoped service.
    /// The keyed service pattern enables multiple REST clients with different configurations in the same application.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSeriRestClient(
    ///     "MyApi",
    ///     (sp, key) => new RestClientOptions("https://api.example.com")
    ///     {
    ///         ThrowOnAnyError = true,
    ///         Timeout = TimeSpan.FromSeconds(30)
    ///     }
    /// );
    ///
    /// // Then inject with:
    /// // [FromKeyedServices("MyApi")] ILoggingRestClient client
    /// </code>
    /// </example>
    public static IServiceCollection AddSeriRestClient(this IServiceCollection services,
           object? key,
           Func<IServiceProvider, object?, RestClientOptions> implementationFactory)
    {
        return services
            .AddKeyedSingleton<RestClientOptions>(key, implementationFactory)
            .AddKeyedScoped<ILoggingRestClient>(key, (sp, serviceKey) =>

                new LoggingRestClient(
                    sp.GetRequiredKeyedService<RestClientOptions>(key),
                    sp.GetRequiredService<ILogger>())
            )
            .AddKeyedScoped(key, (sp, serviceKey) =>

                new LoggingRestClient(
                    sp.GetRequiredKeyedService<RestClientOptions>(key),
                    sp.GetRequiredService<ILogger>())
            );
    }
}
