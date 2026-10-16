using Microsoft.Extensions.DependencyInjection;
using RestSharp;

namespace Zyga.SeriRestClient;

/// <summary>
/// Fluent configuration methods for <see cref="ISeriRestClientBuilder"/>.
/// </summary>
public static class SeriRestClientBuilderExtensions
{
    /// <summary>Sets the <see cref="RestClientOptions.BaseUrl"/> for this client.</summary>
    /// <param name="builder">The builder returned by <see cref="ServiceCollectionExtensions.AddSeriRestClient"/>.</param>
    /// <param name="baseUrl">The base URL for the REST client.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static ISeriRestClientBuilder WithBaseUrl(this ISeriRestClientBuilder builder, string baseUrl) =>
        builder.Configure((_, _, options) => options.BaseUrl = new Uri(baseUrl));

    /// <summary>Sets the <see cref="RestClientOptions.Timeout"/> for this client.</summary>
    /// <param name="builder">The builder returned by <see cref="ServiceCollectionExtensions.AddSeriRestClient"/>.</param>
    /// <param name="timeout">The request timeout.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static ISeriRestClientBuilder WithTimeout(this ISeriRestClientBuilder builder, TimeSpan timeout) =>
        builder.Configure((_, _, options) => options.Timeout = timeout);

    /// <summary>
    /// Runs an arbitrary configuration action against the <see cref="RestClientOptions"/> at
    /// resolution time, with access to the <see cref="IServiceProvider"/> and the service key.
    /// Use this for any <see cref="RestClientOptions"/> property not covered by a dedicated
    /// <c>With*</c> method.
    /// </summary>
    /// <param name="builder">The builder returned by <see cref="ServiceCollectionExtensions.AddSeriRestClient"/>.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static ISeriRestClientBuilder Configure(
        this ISeriRestClientBuilder builder,
        Action<IServiceProvider, object?, RestClientOptions> configure)
    {
        ((SeriRestClientBuilder)builder).AddConfigureAction(configure);
        return builder;
    }
}
