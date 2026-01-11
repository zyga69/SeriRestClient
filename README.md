# SeriRestClient

A logging-enhanced REST client library that wraps RestSharp with integrated Serilog logging capabilities for comprehensive HTTP request and response logging.


## Features

- 🔍 **Comprehensive HTTP Logging** - Automatically logs all request and response details
- 📝 **Pretty Formatting** - JSON and XML payloads are automatically formatted for readability
- 🍪 **Cookie Tracking** - Logs all cookies sent and received
- ⚡ **Performance Metrics** - Records request duration
- 🎯 **Keyed Services** - Supports multiple REST clients with different configurations
- 🔌 **DI Integration** - Easy integration with Microsoft.Extensions.DependencyInjection

## Installation

```bash
dotnet add package SeriRestClient
```

## Quick Start

### 1. Register the Service

```csharp
using SeriRestClient;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration));

// Register SeriRestClient with a keyed service
builder.Services.AddSeriRestClient(
    "MyApi",
    (sp, key) => new RestClientOptions("https://api.example.com")
    {
        ThrowOnAnyError = false,
        Timeout = TimeSpan.FromSeconds(30)
    }
);
```

### 2. Inject and Use

```csharp
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using SeriRestClient;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly ISeriRestClient _client;

    public UsersController([FromKeyedServices("MyApi")] ISeriRestClient client)
    {
        _client = client;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var request = new RestRequest("users/{id}");
        request.AddUrlSegment("id", id);
        
        var response = _client.LogRequest<User>(request, Method.Get);
        
        if (response.IsSuccessful)
            return Ok(response.Data);
            
        return StatusCode((int)response.StatusCode, response.ErrorMessage);
    }
}
```

## Logging Output

SeriRestClient logs detailed information at different levels:

- **Debug**: Request/response details, headers, cookies, body
- **Warning**: Non-success status codes with successful HTTP completion
- **Error**: Failed HTTP requests

Example log output:
```
[DBG] REQUEST  [Get][https://api.example.com/users/123]
[DBG]  [QueryParameter]->[limit]: [10]
[DBG]  [Cookie]->[session]: [abc123]
[DBG] RESPONSE [OK]
[DBG]  [Payload]: {
  "id": 123,
  "name": "John Doe",
  "email": "john@example.com"
}
[DBG] REQUEST -> RESPONSE: 00:00:01.2345678
```

## Multiple REST Clients

Register multiple clients with different configurations:

```csharp
services.AddSeriRestClient(
    "UsersApi",
    (sp, key) => new RestClientOptions("https://users.api.example.com"));

services.AddSeriRestClient(
    "OrdersApi", 
    (sp, key) => new RestClientOptions("https://orders.api.example.com"));

// Inject specific clients
public MyService(
    [FromKeyedServices("UsersApi")] ISeriRestClient usersClient,
    [FromKeyedServices("OrdersApi")] ISeriRestClient ordersClient)
{
    // Use different clients for different APIs
}
```

## Configuration

### Accessing Base RestClient Methods

If you need access to the full RestSharp `RestClient` API, inject the concrete type instead of the interface:

```csharp
public MyService([FromKeyedServices("MyApi")] SeriRestClient client)
{
    // Access all RestClient methods
    var response = client.ExecuteAsync(request);
}
```

### Customizing Log Levels

Configure Serilog to control logging levels:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "SeriRestClient": "Debug"
      }
    }
  }
}
```

## Requirements

- .NET 10.0 or higher
- RestSharp 112.1.0+
- Serilog 4.2.0+

## License

MIT License - see [LICENSE](LICENSE) for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Version

This package uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for semantic versioning.
