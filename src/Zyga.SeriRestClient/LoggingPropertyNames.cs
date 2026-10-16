namespace Zyga.SeriRestClient;

/// <summary>
/// Names of the Serilog properties <see cref="LoggingRestClient"/> attaches to its
/// payload log events, shared as a public contract so a custom formatter (in-process
/// or in a separate package) doesn't need to hardcode magic strings.
/// </summary>
public static class LoggingPropertyNames
{
    /// <summary>The Serilog message-template property carrying the formatted payload string.</summary>
    public const string Payload = "Payload";

    /// <summary>The Serilog context property (via <c>ForContext</c>) carrying the payload's <see cref="PayloadFormat"/>.</summary>
    public const string PayloadFormat = "PayloadFormat";
}
