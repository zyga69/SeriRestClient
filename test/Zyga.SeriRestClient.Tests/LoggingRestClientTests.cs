using System.Net;
using RestSharp;
using Serilog.Events;
using Xunit;

namespace Zyga.SeriRestClient.Tests;

public class LoggingRestClientTests
{
    private const string Json = "{\"id\":1,\"name\":\"ada\"}";
    private const string Xml = "<user><id>1</id><name>ada</name></user>";

    [Fact]
    public void Successful_response_is_logged_at_debug()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            var response = client.LogRequest<object>(new RestRequest("users"), Method.Get);

            Assert.True(response.IsSuccessful);
            var line = Assert.Single(sink.Events, e => e.RenderMessage().StartsWith("RESPONSE "));
            Assert.Equal(LogEventLevel.Debug, line.Level);
            Assert.Contains("OK", line.RenderMessage());
        }
    }

    [Fact]
    public void Failed_response_is_logged_at_error()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.InternalServerError, "boom", "text/plain");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var line = Assert.Single(sink.Events, e => e.RenderMessage().StartsWith("RESPONSE "));
            Assert.Equal(LogEventLevel.Error, line.Level);
        }
    }

    [Fact]
    public void Source_context_is_enriched_with_the_base_url()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var context = sink.Events[0].Properties["SourceContext"].ToString();
            Assert.Equal("\"REST: https://api.test/\"", context);
        }
    }

    [Fact]
    public void Request_duration_is_logged()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            Assert.Contains(sink.Messages, m => m.StartsWith("REQUEST -> RESPONSE: "));
        }
    }

    [Fact]
    public void Query_parameters_are_logged_by_type_name_and_value()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            var request = new RestRequest("users");
            request.AddQueryParameter("limit", "10");

            client.LogRequest<object>(request, Method.Get);

            Assert.Contains(sink.Messages, m => m.Contains("[limit]") && m.Contains("[10]"));
        }
    }

    [Fact]
    public void Json_response_body_is_pretty_printed()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Contains("\n", payload);
        }
    }

    [Fact]
    public void Xml_response_body_is_pretty_printed()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Xml, "application/xml");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Contains("\n", payload);
        }
    }

    [Fact]
    public void Unknown_content_type_passes_the_body_through_unchanged()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "text/plain");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Equal(" [Payload]: " + Json, payload);
        }
    }

    // A charset suffix does NOT defeat the formatter: RestSharp strips media-type
    // parameters, so response.ContentType is "application/json" here.
    [Fact]
    public void Json_with_a_charset_suffix_is_still_pretty_printed()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json; charset=utf-8");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Contains("\n", payload);
        }
    }

    // CHARACTERIZATION TEST — pins current behaviour, which is arguably wrong.
    // LogResponseBody matches ContentType exactly, so a structured-syntax "+json"
    // subtype falls through unformatted. See the spec's Known issues.
    [Fact]
    public void Vendor_json_subtype_is_not_pretty_printed()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/vnd.api+json");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Equal(" [Payload]: " + Json, payload);
        }
    }

    [Fact]
    public void Json_request_body_is_logged_indented()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            var request = new RestRequest("users");
            request.AddJsonBody(new { id = 1, name = "ada" });

            client.LogRequest<object>(request, Method.Post);

            Assert.Contains(sink.Messages, m => m.StartsWith(" [Payload]: {") && m.Contains("\n"));
        }
    }

    [Fact]
    public void Json_response_payload_is_tagged_with_PayloadFormat_Json()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payloadEvent = Assert.Single(sink.Events, e => e.Properties.ContainsKey(LoggingPropertyNames.Payload));

            var format = Assert.IsType<ScalarValue>(payloadEvent.Properties[LoggingPropertyNames.PayloadFormat]);
            Assert.Equal(PayloadFormat.Json, format.Value);

            var payload = Assert.IsType<ScalarValue>(payloadEvent.Properties[LoggingPropertyNames.Payload]);
            Assert.Contains("\n", Assert.IsType<string>(payload.Value));
        }
    }

    [Fact]
    public void Xml_response_payload_is_tagged_with_PayloadFormat_Xml()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Xml, "application/xml");
        using (scope)
        {
            client.LogRequest<object>(new RestRequest("users"), Method.Get);

            var payloadEvent = Assert.Single(sink.Events, e => e.Properties.ContainsKey(LoggingPropertyNames.Payload));

            var format = Assert.IsType<ScalarValue>(payloadEvent.Properties[LoggingPropertyNames.PayloadFormat]);
            Assert.Equal(PayloadFormat.Xml, format.Value);
        }
    }

    [Fact]
    public void Json_request_payload_is_tagged_with_PayloadFormat_Json()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, Json, "application/json");
        using (scope)
        {
            var request = new RestRequest("users");
            request.AddJsonBody(new { id = 1, name = "ada" });

            client.LogRequest<object>(request, Method.Post);

            var payloadEvents = sink.Events.Where(e => e.Properties.ContainsKey(LoggingPropertyNames.Payload)).ToList();
            Assert.Equal(2, payloadEvents.Count); // request body + response body

            var requestFormat = Assert.IsType<ScalarValue>(payloadEvents[0].Properties[LoggingPropertyNames.PayloadFormat]);
            Assert.Equal(PayloadFormat.Json, requestFormat.Value);
        }
    }

    // Regression test: a response labeled Content-Type: application/xml but carrying a body
    // that is NOT well-formed XML (an error page from a proxy, a truncated body, a device
    // returning plain text under the wrong content-type) must not crash the request. Logging
    // is a side effect of LogRequest<T> and must never be able to change whether a request
    // completes.
    // Note: these assert IsSuccessStatusCode (the real HTTP status was 2xx), not
    // IsSuccessful — RestSharp's IsSuccessful also factors in whether the body could be
    // deserialized into TResponse, which a malformed body legitimately fails regardless of
    // this fix (confirmed empirically). What this fix guarantees is that LogRequest<T>
    // returns a RestResponse at all, with the real HTTP status, instead of throwing.
    [Fact]
    public void Malformed_xml_response_does_not_crash_the_request()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, "not valid xml <<<", "application/xml");
        using (scope)
        {
            var response = client.LogRequest<object>(new RestRequest("users"), Method.Get);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Equal(" [Payload]: not valid xml <<<", payload);
        }
    }

    // Same bug class as above, via JsonConvert.DeserializeObject in PrettyJson.
    [Fact]
    public void Malformed_json_response_does_not_crash_the_request()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.OK, "not valid json {{{", "application/json");
        using (scope)
        {
            var response = client.LogRequest<object>(new RestRequest("users"), Method.Get);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = Assert.Single(sink.Messages, m => m.StartsWith(" [Payload]: "));
            Assert.Equal(" [Payload]: not valid json {{{", payload);
        }
    }

    // A non-2xx response with a malformed body must also not crash — IsSuccessful should
    // reflect the real HTTP status, not an exception escaping LogRequest<T>.
    [Fact]
    public void Malformed_xml_response_on_an_error_status_still_returns_the_real_status()
    {
        var (client, sink, scope) = Harness.Client(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html", "application/xml");
        using (scope)
        {
            var response = client.LogRequest<object>(new RestRequest("users"), Method.Get);

            Assert.False(response.IsSuccessful);
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
    }
}
