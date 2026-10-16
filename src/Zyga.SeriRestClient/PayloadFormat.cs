namespace Zyga.SeriRestClient;

/// <summary>
/// Identifies how a logged request or response payload was formatted, so a
/// custom Serilog formatter (such as the one in <c>Zyga.SeriRestClient.Spectre</c>)
/// can decide how to render it without re-parsing content types.
/// </summary>
public enum PayloadFormat
{
    /// <summary>The payload was logged as plain, unformatted text.</summary>
    Text,

    /// <summary>The payload was pretty-printed as JSON.</summary>
    Json,

    /// <summary>The payload was pretty-printed as XML.</summary>
    Xml
}
