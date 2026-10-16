using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using Xunit;

namespace Zyga.SeriRestClient.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Resolves_both_interface_and_concrete_type_by_key()
    {
        var (provider, _) = Harness.Build(new StubHandler(HttpStatusCode.OK, "{}", "application/json"));
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<ILoggingRestClient>(Harness.Key));
        Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<LoggingRestClient>(Harness.Key));
    }

    // CHARACTERIZATION TEST — pins current behaviour, which is arguably wrong.
    // The interface and the concrete type are separate registrations with separate
    // factories, so one scope yields two distinct clients. See the spec's Known issues.
    [Fact]
    public void Interface_and_concrete_registrations_yield_distinct_instances()
    {
        var (provider, _) = Harness.Build(new StubHandler(HttpStatusCode.OK, "{}", "application/json"));
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredKeyedService<ILoggingRestClient>(Harness.Key);
        var viaConcrete = scope.ServiceProvider.GetRequiredKeyedService<LoggingRestClient>(Harness.Key);

        Assert.NotSame(viaInterface, viaConcrete);
    }

    [Fact]
    public void Options_are_registered_as_a_keyed_singleton()
    {
        var (provider, _) = Harness.Build(new StubHandler(HttpStatusCode.OK, "{}", "application/json"));

        var first = provider.GetRequiredKeyedService<RestClientOptions>(Harness.Key);
        var second = provider.GetRequiredKeyedService<RestClientOptions>(Harness.Key);

        Assert.Same(first, second);
    }

    [Fact]
    public void Separate_keys_produce_clients_with_their_own_base_urls()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}", "application/json");
        var services = new ServiceCollection();
        services.AddSingleton<Serilog.ILogger>(new Serilog.LoggerConfiguration().CreateLogger());
        services.AddSeriRestClient("users")
            .WithBaseUrl("https://users.test")
            .Configure((_, _, options) => options.ConfigureMessageHandler = _ => handler);
        services.AddSeriRestClient("orders")
            .WithBaseUrl("https://orders.test")
            .Configure((_, _, options) => options.ConfigureMessageHandler = _ => handler);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var users = scope.ServiceProvider.GetRequiredKeyedService<LoggingRestClient>("users");
        var orders = scope.ServiceProvider.GetRequiredKeyedService<LoggingRestClient>("orders");

        Assert.Equal("https://users.test/", users.Options.BaseUrl!.ToString());
        Assert.Equal("https://orders.test/", orders.Options.BaseUrl!.ToString());
    }

    [Fact]
    public void Resolution_fails_when_no_non_keyed_serilog_logger_is_registered()
    {
        var services = new ServiceCollection();
        services.AddSeriRestClient(Harness.Key).WithBaseUrl(Harness.BaseUrl);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredKeyedService<ILoggingRestClient>(Harness.Key));
    }

    [Fact]
    public void Builder_applies_WithTimeout()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Serilog.ILogger>(new Serilog.LoggerConfiguration().CreateLogger());
        services.AddSeriRestClient(Harness.Key)
            .WithBaseUrl(Harness.BaseUrl)
            .WithTimeout(TimeSpan.FromSeconds(42));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredKeyedService<RestClientOptions>(Harness.Key);

        Assert.Equal(TimeSpan.FromSeconds(42), options.Timeout);
    }
}
