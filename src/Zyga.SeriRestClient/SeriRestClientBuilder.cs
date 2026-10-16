using Microsoft.Extensions.DependencyInjection;
using RestSharp;

namespace Zyga.SeriRestClient;

internal sealed class SeriRestClientBuilder : ISeriRestClientBuilder
{
    private readonly List<Action<IServiceProvider, object?, RestClientOptions>> configureActions = [];

    public SeriRestClientBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }

    public SeriRestClientBuilder AddConfigureAction(Action<IServiceProvider, object?, RestClientOptions> action)
    {
        configureActions.Add(action);
        return this;
    }

    public RestClientOptions Build(IServiceProvider serviceProvider, object? key)
    {
        var options = new RestClientOptions();

        foreach (var action in configureActions)
        {
            action(serviceProvider, key, options);
        }

        return options;
    }
}
