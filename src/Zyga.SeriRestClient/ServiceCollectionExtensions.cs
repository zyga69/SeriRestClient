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
    /// in the dependency injection container, and returns a builder for configuring the client's
    /// <see cref="RestClientOptions"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="key">The service key used to identify this specific REST client instance.
    /// This allows multiple REST clients to be registered with different configurations.</param>
    /// <returns>A builder for configuring this client's <see cref="RestClientOptions"/>.</returns>
    /// <remarks>
    /// This method registers the REST client options as a keyed singleton and the client itself as a keyed scoped service.
    /// The keyed service pattern enables multiple REST clients with different configurations in the same application.
    /// Calling <c>WithBaseUrl</c> on the returned builder is not enforced: if it is never called, the resulting
    /// <see cref="RestClientOptions"/> is built with a null <see cref="RestClientOptions.BaseUrl"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSeriRestClient("MyApi")
    ///     .WithBaseUrl("https://api.example.com")
    ///     .WithTimeout(TimeSpan.FromSeconds(30));
    ///
    /// // Then inject with:
    /// // [FromKeyedServices("MyApi")] ILoggingRestClient client
    /// </code>
    /// </example>
    public static ISeriRestClientBuilder AddSeriRestClient(this IServiceCollection services, object? key)
    {
        var builder = new SeriRestClientBuilder(services);

        services
            .AddKeyedSingleton<RestClientOptions>(key, (sp, serviceKey) => builder.Build(sp, serviceKey))
            .AddKeyedScoped<ILoggingRestClient>(key, (sp, serviceKey) =>
                new LoggingRestClient(
                    sp.GetRequiredKeyedService<RestClientOptions>(key),
                    sp.GetRequiredService<ILogger>()))
            .AddKeyedScoped(key, (sp, serviceKey) =>
                new LoggingRestClient(
                    sp.GetRequiredKeyedService<RestClientOptions>(key),
                    sp.GetRequiredService<ILogger>()));

        return builder;
    }
}
