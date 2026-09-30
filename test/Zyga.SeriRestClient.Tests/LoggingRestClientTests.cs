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
}
