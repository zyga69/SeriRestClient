# Zyga.SeriRestClient

[![CI](https://github.com/zyga69/SeriRestClient/actions/workflows/ci.yml/badge.svg)](https://github.com/zyga69/SeriRestClient/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Zyga.SeriRestClient.svg)](https://www.nuget.org/packages/Zyga.SeriRestClient)
[![Downloads](https://img.shields.io/nuget/dt/Zyga.SeriRestClient.svg)](https://www.nuget.org/packages/Zyga.SeriRestClient)
[![codecov](https://codecov.io/gh/zyga69/SeriRestClient/branch/main/graph/badge.svg)](https://codecov.io/gh/zyga69/SeriRestClient)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A logging-enhanced REST client library that wraps RestSharp with integrated Serilog logging capabilities for comprehensive HTTP request and response logging.

## Features

- 🔍 **Comprehensive HTTP Logging** - Automatically logs all request and response details
- 📝 **Pretty Formatting** - JSON and XML payloads are automatically formatted for readability
- 🍪 **Cookie Tracking** - Logs all cookies sent and received
- ⚡ **Performance Metrics** - Records request duration
- 🎯 **Keyed Services** - Supports multiple REST clients with different configurations
- 🔌 **DI Integration** - Easy integration with Microsoft.Extensions.DependencyInjection

## Install

```bash
dotnet add package Zyga.SeriRestClient
```

Targets .NET 8, .NET 9 and .NET 10.

## Quick Start

### 1. Register the Service

```csharp
using Zyga.SeriRestClient;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Register the client with a keyed service
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
using Zyga.SeriRestClient;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILoggingRestClient _client;

    public UsersController([FromKeyedServices("MyApi")] ILoggingRestClient client)
    {
        _client = client;
    }

    [HttpGet("{id}")]
    public IActionResult GetUser(int id)
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

The client logs detailed information at different levels:

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
    [FromKeyedServices("UsersApi")] ILoggingRestClient usersClient,
    [FromKeyedServices("OrdersApi")] ILoggingRestClient ordersClient)
{
    // Use different clients for different APIs
}
```

## Configuration

### Accessing Base RestClient Methods

If you need access to the full RestSharp `RestClient` API, inject the concrete type instead of the interface:

```csharp
public MyService([FromKeyedServices("MyApi")] LoggingRestClient client)
{
    // Access all RestClient methods
    var response = client.ExecuteAsync(request);
}
```

Note that the interface and concrete-type registrations are separate DI entries with
separate factories — resolving both under the same key in one scope gives you two
distinct client instances, not the same one seen through two types.

### Customizing Log Levels

Each client's logger is enriched with a `SourceContext` of the exact form
`"REST: {BaseUrl}"` — the literal text `REST: ` followed by the client's base URL,
including the trailing slash `RestClientOptions.BaseUrl` adds because it is a `Uri`.

**A targeted override must be set in code**, not `appsettings.json`. Two things rule
out a JSON override: Serilog only matches a `MinimumLevel.Override` key as an exact or
dot-segmented prefix of `SourceContext`, and `REST: {BaseUrl}` has no dot segment after
`REST` — so `"SeriRestClient"`, `"REST"` and `"REST:"` all match nothing. And even the
complete, correct string can't be written in `appsettings.json`, because it contains a
colon, which `Microsoft.Extensions.Configuration` treats as its own section separator
and splits before Serilog ever sees the key.

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // The key must be the client's complete SourceContext: the literal text "REST: "
    // followed by the base URL, including the trailing slash. One entry per registered client.
    .MinimumLevel.Override("REST: https://api.example.com/", LogEventLevel.Debug)
    .WriteTo.Console()
    .CreateLogger();
```

If your configuration lives entirely in `appsettings.json`, you can raise the global
minimum level instead — this affects every logger in the process, not just this client:

```json
{
  "Serilog": {
    "MinimumLevel": { "Default": "Debug" }
  }
}
```

## Requirements

- .NET 8, .NET 9, or .NET 10
- RestSharp 114.0.0+
- Serilog 4.4.0+

## Documentation

See [CLAUDE.md](CLAUDE.md) for architecture notes and logging internals.

## Contributing

Issues and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE)

## Version

This package uses [MinVer](https://github.com/adamralph/minver) for semantic versioning,
derived from `v`-prefixed git tags (e.g. `v1.2.3`). There is no `<Version>` element
anywhere in the source — maintainers cut a release by pushing a tag.
