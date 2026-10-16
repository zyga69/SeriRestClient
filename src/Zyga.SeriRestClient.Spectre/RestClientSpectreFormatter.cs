using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Spectre.Console;
using Spectre.Console.Json;
using Zyga.SeriRestClient;

// This file lives inside the `Zyga.SeriRestClient.Spectre` namespace, so a fully-qualified
// `Spectre.Console.Foo` reference here would NOT resolve to the Spectre.Console NuGet
// package's root namespace — `Spectre` binds to this enclosing namespace segment first.
// Everything from the Spectre.Console package must come in via `using` (as above); do not
// try to fully-qualify a Spectre type in this file.
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

    // Built via char concatenation (NUL, text, NUL) rather than a const string with
    // literal C# escape sequences for the NUL (U+0000) and ESC (U+001B) control characters:
    // this repo's file-editing tooling has previously been observed to corrupt such literal
    // escape sequences into raw control bytes when writing source files, which silently
    // breaks the sentinel. Keep this construction as-is rather than simplifying it back
    // into a literal-escape const.
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

        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            properties,
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default);
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

        // A StringWriter-backed output is never attached to a real terminal, so Spectre
        // falls back to a default profile width of 80 and hard-wraps long rendered lines —
        // inserting real newlines into the middle of long string values. Disable wrapping.
        ansiConsole.Profile.Width = int.MaxValue;

        try
        {
            ansiConsole.Write(new JsonText(json));
        }
        catch (InvalidOperationException)
        {
            // LoggingRestClient only ever tags PayloadFormat.Json after round-tripping the
            // payload through Newtonsoft, so this isn't reachable from this library's own
            // output. But LoggingPropertyNames is a public contract third-party producers can
            // also use, and they have no such guarantee. Serilog silently swallows sink
            // exceptions, so fall back to the raw text instead of losing the log line.
            writer.Write(json);
        }

        return writer.ToString();
    }
}
