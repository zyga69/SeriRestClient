using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using RestSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Xunit;

namespace Zyga.SeriRestClient.Spectre.Tests;

public class RestClientSpectreFormatterTests
{
    // A string value longer than Spectre's default 80-column fallback profile width, used to
    // detect hard-wrapping (Finding 1) without relying on a literal escape sequence of its own.
    private const string LongToken = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghij";

    private static LogEvent CreatePayloadEvent(
        string payload,
        PayloadFormat format,
        ActivityTraceId? traceId = null,
        ActivitySpanId? spanId = null)
    {
        var template = new MessageTemplateParser().Parse(" [Payload]: {Payload:l}");
        var properties = new[]
        {
            new LogEventProperty(LoggingPropertyNames.Payload, new ScalarValue(payload)),
            new LogEventProperty(LoggingPropertyNames.PayloadFormat, new ScalarValue(format))
        };

        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Debug,
            null,
            template,
            properties,
            traceId ?? default,
            spanId ?? default);
    }

    [Fact]
    public void Json_payload_is_rendered_with_ansi_escape_codes_and_long_strings_survive_intact()
    {
        var formatter = new RestClientSpectreFormatter();
        var json = "{\"id\":1,\"token\":\"" + LongToken + "\"}";
        var logEvent = CreatePayloadEvent(json, PayloadFormat.Json);

        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);

        var rendered = writer.ToString();
        Assert.Contains((char)0x1b, rendered);

        // The long string must survive completely unbroken: Spectre's default 80-column
        // fallback profile width would otherwise hard-wrap it with inserted newlines.
        Assert.Contains(LongToken, rendered);
    }

    [Fact]
    public void Malformed_json_payload_does_not_throw_and_falls_back_to_raw_text()
    {
        var formatter = new RestClientSpectreFormatter();
        const string malformed = "{ not valid json";
        var logEvent = CreatePayloadEvent(malformed, PayloadFormat.Json);

        using var writer = new StringWriter();

        formatter.Format(logEvent, writer);

        Assert.Contains(malformed, writer.ToString());
    }

    [Fact]
    public void Xml_payload_renders_identically_to_the_plain_formatter()
    {
        var formatter = new RestClientSpectreFormatter();
        var plain = new MessageTemplateTextFormatter(RestClientSpectreFormatter.DefaultOutputTemplate);
        var logEvent = CreatePayloadEvent("<id>1</id>", PayloadFormat.Xml);

        using var actual = new StringWriter();
        formatter.Format(logEvent, actual);

        using var expected = new StringWriter();
        plain.Format(logEvent, expected);

        Assert.Equal(expected.ToString(), actual.ToString());
    }

    [Fact]
    public void Non_payload_event_renders_identically_to_the_plain_formatter()
    {
        var formatter = new RestClientSpectreFormatter();
        var plain = new MessageTemplateTextFormatter(RestClientSpectreFormatter.DefaultOutputTemplate);
        var template = new MessageTemplateParser().Parse("REQUEST [{Method}][{Url}]");
        var properties = new[]
        {
            new LogEventProperty("Method", new ScalarValue("GET")),
            new LogEventProperty("Url", new ScalarValue("https://api.test/users"))
        };
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, null, template, properties);

        using var actual = new StringWriter();
        formatter.Format(logEvent, actual);

        using var expected = new StringWriter();
        plain.Format(logEvent, expected);

        Assert.Equal(expected.ToString(), actual.ToString());
    }

    [Fact]
    public void Json_payload_preserves_trace_and_span_ids()
    {
        const string outputTemplate = "{TraceId} {SpanId} {Message:lj}{NewLine}{Exception}";
        var formatter = new RestClientSpectreFormatter(outputTemplate);
        var plain = new MessageTemplateTextFormatter(outputTemplate);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var logEvent = CreatePayloadEvent("{\"id\":1}", PayloadFormat.Json, traceId, spanId);

        using var actual = new StringWriter();
        formatter.Format(logEvent, actual);

        using var expected = new StringWriter();
        plain.Format(logEvent, expected);

        // Can't compare the two renderings wholesale (one highlights the JSON payload, the
        // other doesn't), but both must render the same {TraceId}/{SpanId} prefix, since those
        // tokens don't depend on the Payload property that differs between the two paths.
        var expectedPrefix = $"{traceId} {spanId} ";
        Assert.StartsWith(expectedPrefix, expected.ToString());
        Assert.StartsWith(expectedPrefix, actual.ToString());
    }

    /// <summary>Captures emitted log events so the end-to-end test can assert on them.</summary>
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>Returns a canned HTTP response without binding a socket.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body, string? contentType)
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

    [Fact]
    public void Real_LoggingRestClient_json_response_survives_the_Spectre_formatter_intact()
    {
        // Exercises the real producer (Zyga.SeriRestClient.LoggingRestClient) through to the
        // real consumer (RestClientSpectreFormatter), proving they agree on property shape in
        // practice -- not just via separate unit tests on each side (Finding 3). This also
        // exercises Finding 1's wrapping fix against the real pipeline, not a synthetic LogEvent.
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var json = "{\"id\":1,\"token\":\"" + LongToken + "\"}";
        var handler = new StubHandler(HttpStatusCode.OK, json, "application/json");
        var options = new RestClientOptions("https://api.test")
        {
            ConfigureMessageHandler = _ => handler
        };

        var client = new LoggingRestClient(options, logger);

        client.LogRequest<object>(new RestRequest("users"), Method.Get);

        var payloadEvent = Assert.Single(sink.Events, e => e.Properties.ContainsKey(LoggingPropertyNames.Payload));

        var formatter = new RestClientSpectreFormatter();
        using var writer = new StringWriter();
        formatter.Format(payloadEvent, writer);

        Assert.Contains(LongToken, writer.ToString());
    }
}
