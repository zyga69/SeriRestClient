# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

SeriRestClient is a small .NET 10 NuGet library that wraps RestSharp's `RestClient` with Serilog
logging of full HTTP request/response detail. It is published to a private BaGet feed, not nuget.org.
See `README.md` for the consumer-facing API and usage examples.

## Layout

The library is a **flat, single-project repo**: `SeriRestClient.csproj` and all three source files
(`SeriRestClient.cs`, `ISeriRestClient.cs`, `IServiceCollectionExtensions.cs`) live at the repo root.
There is no `src/` directory.

## Commands

```bash
dotnet build -c Release                                   # builds the library
dotnet test SeriRestClient.Tests/SeriRestClient.Tests.csproj   # must name the test project explicitly
dotnet pack SeriRestClient.csproj -c Release -o ./nupkgs  # produces .nupkg + .snupkg
nbgv get-version -p . -f json                             # shows the version that will be stamped
```

Run a single test once tests exist: `dotnet test SeriRestClient.Tests/SeriRestClient.Tests.csproj --filter "FullyQualifiedName~MyTestName"`

### Build/test gotchas

- **A bare `dotnet test` at the repo root silently runs nothing.** It resolves to `SeriRestClient.sln`,
  which contains only the library project — the test project was never added to the solution.
- `SeriRestClient.Tests` currently contains **no test files and no `ProjectReference` to the library**.
  Adding real tests requires adding that reference (and ideally adding the project to the `.sln`).
- Version numbers come from Nerdbank.GitVersioning (`version.json` + `Directory.Build.props`), derived
  from git height. A shallow clone yields wrong versions — CI uses `clone: depth: 0`.

## Architecture

**Inheritance, not composition.** `SeriRestClient : RestClient, ISeriRestClient`. This is deliberate:
consumers who inject the interface get only the logging wrapper (`LogRequest<T>`), while consumers who
inject the concrete `SeriRestClient` type also get the entire RestSharp `RestClient` surface
(`ExecuteAsync`, etc.). Keep both injection paths working when changing the class.

**Keyed DI registration** (`IServiceCollectionExtensions.AddSeriRestClient`) is what enables multiple
independently-configured clients in one app:

- `RestClientOptions` → keyed **singleton**, built by the caller's factory.
- `ISeriRestClient` → keyed **scoped**.
- `SeriRestClient` (concrete) → keyed **scoped**, registered *separately*.

Because the interface and the concrete type are two registrations with two factories, resolving both
under the same key in one scope produces **two distinct client instances**. Anything that must be
shared per scope (cookie containers, connection state) will not be shared across them.

The extension also requires a **non-keyed `Serilog.ILogger`** in the container
(`sp.GetRequiredService<ILogger>()`), so `UseSerilog(...)` or an equivalent registration is a
precondition for resolution to succeed.

**Logging behavior** (`SeriRestClient.LogRequest<T>`):

- The logger is enriched with `SourceContext = "REST: {BaseUrl}"` per client instance. Serilog
  `MinimumLevel.Override` keys must match that prefix — note the README's `"SeriRestClient"` override
  example does *not* match it.
- Levels: `Debug` for request/response detail and timing; `Warning` when the HTTP call completed but
  `IsSuccessful` is false; `Error` when the HTTP call itself failed.
- Bodies are pretty-printed — request bodies by `RestRequest.RequestFormat` (`DataFormat.Json` via
  Newtonsoft, `DataFormat.Xml` via `XmlSerializer`), response bodies by an **exact** match on
  `response.ContentType` against `application/json` / `application/xml`. A charset suffix
  (`application/json; charset=utf-8`) or a `+json` subtype falls through to the unformatted default.
- `LogRequest<T>` is **synchronous** (it calls `Execute<T>`, not `ExecuteAsync`) despite the `await` in
  the XML doc example on `ISeriRestClient`.
- Everything is logged verbatim, including cookies and header values. There is no redaction.

## CI (`.drone.yml`)

Two Drone pipelines on the `mcr.microsoft.com/dotnet/sdk:10.0` image:

- **build** — runs on pushes to any branch *except* `master`/`main`: restore, build, test.
- **publish-nuget** — runs on `master`/`main`: build → test → pack → `dotnet nuget push` to the BaGet
  feed at the `BAGET_URL` secret.

Note both pipelines invoke the bare `dotnet test`, so CI is not currently executing tests either.

## Conventions

`.editorconfig` is authoritative: 4-space indent for C#, file-scoped namespaces, Allman braces
(newline before `{`, `else`, `catch`, `finally`), system usings sorted first. Nullable reference types
and implicit usings are enabled in both projects. Public members carry full XML doc comments
(`GenerateDocumentationFile` is on) — match that when adding public API.
