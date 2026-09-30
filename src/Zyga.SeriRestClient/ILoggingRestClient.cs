using RestSharp;

namespace Zyga.SeriRestClient;

/// <summary>
/// Defines a REST client with integrated Serilog logging capabilities.
/// Provides logging for both HTTP requests and responses including headers, body, and cookies.
/// </summary>
public interface ILoggingRestClient
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
    /// Request and response detail is logged at Debug. A completed call whose response is
    /// unsuccessful logs at Warning; a call that failed outright logs at Error. Bodies are
    /// formatted for readability. Cookies and header values are logged verbatim — apply log
    /// filtering if that matters for your deployment.
    /// </remarks>
    /// <example>
    /// <code>
    /// var request = new RestRequest("api/users/{id}");
    /// request.AddUrlSegment("id", 123);
    /// var response = client.LogRequest&lt;User&gt;(request, Method.Get);
    /// </code>
    /// </example>
    RestResponse<TResponse> LogRequest<TResponse>(RestRequest request, Method method);
}
