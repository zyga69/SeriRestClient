using Microsoft.Extensions.DependencyInjection;

namespace Zyga.SeriRestClient;

/// <summary>
/// Fluent builder returned by <see cref="ServiceCollectionExtensions.AddSeriRestClient"/>,
/// used to configure the <see cref="RestSharp.RestClientOptions"/> for one keyed REST client.
/// </summary>
public interface ISeriRestClientBuilder
{
    /// <summary>The service collection the client was registered into.</summary>
    IServiceCollection Services { get; }
}
