using RestSharp;

namespace SeriRestClient;

/// <summary>
/// Defines a REST client with integrated Serilog logging capabilities.
/// Provides logging for both HTTP requests and responses including headers, body, and cookies.
/// </summary>
public interface ISeriRestClient
{
    /// <summary>
    /// Executes an HTTP request with comprehensive Serilog logging.
    /// Logs request details (method, URL, parameters, body, cookies) before execution
    /// and response details (status code, cookies, body) after execution.
    /// </summary>
    /// <typeparam name="TResponse">The type to deserialize the response into.</typeparam>
    /// <param name="request">The REST request to execute.</param>
    /// <param name="method">The HTTP method to use (GET, POST, PUT, DELETE, etc.).</param>
    /// <returns>A <see cref="RestResponse{TResponse}"/> containing the deserialized response data and metadata.</returns>
    /// <remarks>
    /// All logging is performed at the Information level. Request and response bodies are formatted
    /// for readability (JSON is pretty-printed, XML is indented). Sensitive data in cookies and headers
    /// will be logged - ensure proper log filtering if needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var request = new RestRequest("api/users/{id}");
    /// request.AddUrlSegment("id", 123);
    /// var response = await client.LogRequest&lt;User&gt;(request, Method.Get);
    /// </code>
    /// </example>
    RestResponse<TResponse> LogRequest<TResponse>(RestRequest request, Method method);
}
