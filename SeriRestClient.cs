using Newtonsoft.Json;
using RestSharp;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ILogger = Serilog.ILogger;

namespace SeriRestClient;

/// <summary>
/// A REST client that wraps RestSharp with integrated Serilog logging capabilities.
/// Logs all HTTP request and response details including headers, parameters, body content, and cookies.
/// </summary>
/// <remarks>
/// This client extends RestSharp's <see cref="RestClient"/> and automatically formats
/// JSON and XML payloads for improved readability in logs. All logging is performed at the
/// Debug level for requests/responses, with Warning level for non-success status codes,
/// and Error level for failed requests.
/// </remarks>
public class SeriRestClient : RestClient, ISeriRestClient
{
    private readonly ILogger logger;

    private static readonly XmlWriterSettings settings = new XmlWriterSettings
    {
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\r\n",
        NewLineHandling = NewLineHandling.Replace
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriRestClient"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the REST client, including base URL and default headers.</param>
    /// <param name="logger">The Serilog logger instance used for logging HTTP traffic.</param>
    /// <remarks>
    /// The logger context is enriched with the base URL from the options to help identify
    /// which REST endpoint logs are associated with.
    /// </remarks>
    public SeriRestClient(RestClientOptions options, ILogger logger) : base(options)
    {
        this.logger = logger.ForContext("SourceContext", $"REST: {options.BaseUrl}");
    }

    /// <inheritdoc />
    public RestResponse<TResponse> LogRequest<TResponse>(RestRequest request, Method method)
    {
        logger.Debug($"REQUEST  [{method}][{Options.BaseUrl}/{request.Resource}]");

        foreach (Parameter parameter in request.Parameters)
        {
            if (parameter.Type == ParameterType.RequestBody)
            {
                LogRequestBody(parameter.Value, request.RequestFormat);
            }
            else
            {
                logger.Debug($" [{parameter.Type}]->[{parameter.Name}]: [{parameter.Value}]");
            }
        }

        LogCookies(request.CookieContainer?.GetAllCookies());

        var requestStart = DateTime.UtcNow;
        var response = this.Execute<TResponse>(request, method);
        var requestTime = DateTime.UtcNow - requestStart;

        if (response.IsSuccessful)
        {
            logger.Debug($"RESPONSE [{response.StatusCode}]");
        }
        else if (response.IsSuccessStatusCode)
        {
            logger.Warning($"RESPONSE [{response.StatusCode}]: [{response.ErrorMessage}]");
        }
        else
        {
            logger.Error($"RESPONSE [{response.StatusCode}]: [{response.ErrorMessage}]");
        }

        LogCookies(response.Cookies);
        LogResponseBody(response.Content, response.ContentType);

        logger.Debug($"REQUEST -> RESPONSE: {requestTime}");
        return response;
    }

    private void LogResponseBody(string? content, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        string formatted = string.Empty;

        if (string.IsNullOrWhiteSpace(contentType)) formatted = content;

        switch (contentType)
        {
            case MediaTypeNames.Application.Xml:
                formatted = PrettyXml(content); break;

            case MediaTypeNames.Application.Json:
                formatted = PrettyJson(content); break;
            default: formatted = content; break;
        }

        logger.Debug($" [Payload]: {formatted}");
    }

    private void LogRequestBody(object? data, DataFormat format)
    {
        if (data == null)
            return;

        string formatted = string.Empty;

        switch (format)
        {
            case DataFormat.Json:
                formatted = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented); break;

            case DataFormat.Xml:
                formatted = GetXml(data); break;

            default:
                formatted = data.ToString() ?? string.Empty; break;
        }

        logger.Debug($" [Payload]: {formatted}");
    }

    private void LogCookies(CookieCollection? collection)
    {
        if (collection != null)
        {
            foreach (Cookie cookie in collection)
            {
                logger.Debug($" [Cookie]->[{cookie.Name}]: [{cookie.Value}]");
            }
        }
    }

    private static string GetXml(object? data)
    {
        if (data == null) return string.Empty;

        XmlSerializer xsSubmit = new XmlSerializer(data.GetType());

        using (var sww = new StringWriter())
        {
            using (XmlWriter writer = XmlWriter.Create(sww, settings))
            {
                xsSubmit.Serialize(writer, data);
                return sww.ToString();
            }
        }
    }

    private static string PrettyXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return string.Empty;

        var stringBuilder = new StringBuilder();

        var element = XElement.Parse(xml);

        var settings = new XmlWriterSettings();
        settings.OmitXmlDeclaration = true;
        settings.Indent = true;
        settings.NewLineOnAttributes = true;

        using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
        {
            element.Save(xmlWriter);
        }

        return stringBuilder.ToString();
    }

    private static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        dynamic? parsedJson = JsonConvert.DeserializeObject(json);
        return JsonConvert.SerializeObject(parsedJson, Newtonsoft.Json.Formatting.Indented);
    }
}
