using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Zyga.SeriRestClient.Tests;

/// <summary>Captures emitted log events so tests can assert on them.</summary>
internal sealed class CapturingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);

    public IEnumerable<string> Messages => Events.Select(e => e.RenderMessage());
}

/// <summary>Returns a canned response without binding a socket.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body, string? contentType)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = new StringContent(body);
        content.Headers.ContentType = contentType is null
            ? null
            : MediaTypeHeaderValue.Parse(contentType);
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }
}

internal static class Harness
{
    public const string Key = "api";
    public const string BaseUrl = "https://api.test";

    public static (ServiceProvider Provider, CapturingSink Sink) Build(
        HttpMessageHandler handler, string baseUrl = BaseUrl)
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddSingleton<Serilog.ILogger>(logger);
        services.AddSeriRestClient(Key, (_, _) =>
            new RestClientOptions(baseUrl) { ConfigureMessageHandler = _ => handler });

        return (services.BuildServiceProvider(), sink);
    }

    public static (ILoggingRestClient Client, CapturingSink Sink, IServiceScope Scope) Client(
        HttpStatusCode status, string body, string? contentType)
    {
        var (provider, sink) = Build(new StubHandler(status, body, contentType));
        var scope = provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredKeyedService<ILoggingRestClient>(Key), sink, scope);
    }
}
