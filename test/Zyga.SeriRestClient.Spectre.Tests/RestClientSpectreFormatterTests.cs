using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Xunit;

namespace Zyga.SeriRestClient.Spectre.Tests;

public class RestClientSpectreFormatterTests
{
    private static LogEvent CreatePayloadEvent(string payload, PayloadFormat format)
    {
        var template = new MessageTemplateParser().Parse(" [Payload]: {Payload:l}");
        var properties = new[]
        {
            new LogEventProperty(LoggingPropertyNames.Payload, new ScalarValue(payload)),
            new LogEventProperty(LoggingPropertyNames.PayloadFormat, new ScalarValue(format))
        };

        return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, null, template, properties);
    }

    [Fact]
    public void Json_payload_is_rendered_with_ansi_escape_codes()
    {
        var formatter = new RestClientSpectreFormatter();
        var logEvent = CreatePayloadEvent("{\"id\":1}", PayloadFormat.Json);

        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);

        Assert.Contains((char)0x1b, writer.ToString());
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
}
