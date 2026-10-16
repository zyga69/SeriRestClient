using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Spectre.Console;
using Spectre.Console.Json;
using Zyga.SeriRestClient;

namespace Zyga.SeriRestClient.Spectre;

/// <summary>
/// A Serilog <see cref="ITextFormatter"/> that syntax-highlights JSON payloads logged by
/// <see cref="Zyga.SeriRestClient.LoggingRestClient"/> using Spectre.Console's JSON renderer.
/// Every other log line (including XML/text payloads) is rendered exactly like a plain
/// Serilog console formatter.
/// </summary>
public sealed class RestClientSpectreFormatter : ITextFormatter
{
    /// <summary>The default Serilog output template, matching Serilog's conventional console default.</summary>
    public const string DefaultOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private static readonly string Sentinel = (char)0 + "__ZYGA_SPECTRE_JSON_PAYLOAD__" + (char)0;

    private readonly MessageTemplateTextFormatter plainFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestClientSpectreFormatter"/> class.
    /// </summary>
    /// <param name="outputTemplate">The Serilog output template for everything except highlighted JSON payloads.</param>
    /// <param name="formatProvider">An optional format provider, passed through to the underlying formatter.</param>
    public RestClientSpectreFormatter(string outputTemplate = DefaultOutputTemplate, IFormatProvider? formatProvider = null)
    {
        plainFormatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (!TryGetJsonPayload(logEvent, out var json))
        {
            plainFormatter.Format(logEvent, output);
            return;
        }

        var sanitized = WithPayloadReplacedBySentinel(logEvent);

        using var buffer = new StringWriter();
        plainFormatter.Format(sanitized, buffer);

        output.Write(buffer.ToString().Replace(Sentinel, RenderJson(json)));
    }

    private static bool TryGetJsonPayload(LogEvent logEvent, out string json)
    {
        json = string.Empty;

        if (!logEvent.Properties.TryGetValue(LoggingPropertyNames.PayloadFormat, out var formatValue) ||
            formatValue is not ScalarValue { Value: PayloadFormat.Json })
        {
            return false;
        }

        if (!logEvent.Properties.TryGetValue(LoggingPropertyNames.Payload, out var payloadValue) ||
            payloadValue is not ScalarValue { Value: string payload })
        {
            return false;
        }

        json = payload;
        return true;
    }

    private static LogEvent WithPayloadReplacedBySentinel(LogEvent logEvent)
    {
        var properties = logEvent.Properties.Select(kv => kv.Key == LoggingPropertyNames.Payload
            ? new LogEventProperty(kv.Key, new ScalarValue(Sentinel))
            : new LogEventProperty(kv.Key, kv.Value));

        return new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, properties);
    }

    private static string RenderJson(string json)
    {
        using var writer = new StringWriter();
        var ansiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new AnsiConsoleOutput(writer),
            Interactive = InteractionSupport.No,
        });

        ansiConsole.Write(new JsonText(json));
        return writer.ToString();
    }
}
