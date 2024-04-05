using Newtonsoft.Json;
using RestSharp;
using Serilog;
using Serilog.Core;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SeriRest
{
    public class SeriRestClient : RestClient
    {
        private readonly ILogger logger;

        private static XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace
        };

        public SeriRestClient(RestClientOptions options, ILogger logger) : base(options)
        {
            this.logger = logger;
        }

        public RestResponse<TResponse> LogRequest<TResponse>(RestRequest request, Method method)
        {
            logger.Debug($"[REST] REQUEST  [{method}][{request.Resource}]");

            foreach (Parameter parameter in request.Parameters)
            {
                if (parameter.Type == ParameterType.RequestBody)
                {
                    LogRequestBody(parameter.Value, request.RequestFormat);
                }
                else
                {
                    logger.Debug($"[REST]  [{parameter.Type}]->[{parameter.Name}]: [{parameter.Value}]");
                }
            }

            LogCookies(request.CookieContainer?.GetAllCookies());

            var response = this.Execute<TResponse>(request, method);

            if (response.IsSuccessful)
            {
                logger.Debug($"[REST] RESPONSE [{response.StatusCode}]");
            }
            else if (response.IsSuccessStatusCode)
            {
                logger.Warning($"[REST] RESPONSE [{response.StatusCode}]: [{response.ErrorMessage}]");
            }
            else
            {
                logger.Error($"[REST] RESPONSE [{response.StatusCode}]: [{response.ErrorMessage}]");
            }

            LogCookies(response.Cookies);
            LogResponseBody(response.Content, response.ContentType);

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

            logger.Debug($"[REST]  [Payload]: {formatted}");
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

            logger.Debug($"[REST]  [Payload]: {formatted}");
        }

        private void LogCookies(CookieCollection? collection)
        {
            if (collection != null)
            {
                foreach (Cookie cookie in collection)
                {
                    logger.Debug($"[REST]  [Cookie]->[{cookie.Name}]: [{cookie.Value}]");
                }
            }
        }

        private static string GetXml(object? data)
        {
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

            dynamic parsedJson = JsonConvert.DeserializeObject(json);
            return JsonConvert.SerializeObject(parsedJson, Newtonsoft.Json.Formatting.Indented);
        }
    }
}