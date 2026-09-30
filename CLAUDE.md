# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Zyga.SeriRestClient is a small NuGet library targeting `net8.0;net9.0;net10.0` that wraps RestSharp's
`RestClient` with Serilog logging of full HTTP request/response detail. It is published to nuget.org.
See `README.md` for the consumer-facing API and usage examples.

## Layout

- `src/Zyga.SeriRestClient/` — the library: `LoggingRestClient.cs`, `ILoggingRestClient.cs`,
  `ServiceCollectionExtensions.cs`, `Zyga.SeriRestClient.csproj`.
- `test/Zyga.SeriRestClient.Tests/` — the test suite, referencing the library via `ProjectReference`.
- `Directory.Build.props` — shared authoring metadata, target frameworks, SourceLink, and the
  `MinVerTagPrefix` setting, applied to every project in the repo.
- `Directory.Packages.props` — central package management; every package version is declared here,
  and every `PackageReference` in every `.csproj` is version-less.
- `global.json` — pins the SDK and opts the repo into the Microsoft.Testing.Platform (MTP) test runner
  via its `test.runner` block. Without this, the test project fails to *build* on SDK 10.

## Commands

```bash
dotnet build -c Release                                    # builds all three target frameworks
dotnet test -c Release -f net10.0                          # runs the suite (see gotcha below)
dotnet pack -c Release -o ./nupkgs                          # produces .nupkg + .snupkg for both projects
```

Run a single test: `dotnet test -c Release -f net10.0 -- --filter-method "*MyTestName*"` (MTP's filter
syntax, not VSTest's `--filter`).

### Build/test gotchas

- **Local test runs need `-f net10.0`.** Only the .NET 10 runtime may be installed locally; the
  net8.0/net9.0 test apps can't launch without their own runtimes, and a bare `dotnet test` exits 150
  with `app-launch-failed`. CI installs all three runtimes and runs the full matrix.
- **Versioning comes from MinVer, not a checked-in file.** There is no `<Version>` element anywhere in
  the repo. Until a `v`-prefixed tag exists, every build produces a `0.0.0-alpha.0` package — that's
  expected, not a fault. A release is cut by pushing a tag (e.g. `v1.2.3`).
- **A versioned `PackageReference` breaks restore under CPM.** `Directory.Packages.props` is the sole
  place a package version may be declared; adding a `Version` attribute on a `PackageReference` in any
  `.csproj` fails restore with `NU1008`.

## Architecture

**Inheritance, not composition.** `LoggingRestClient : RestClient, ILoggingRestClient`. This is
deliberate: consumers who inject the interface get only the logging wrapper (`LogRequest<T>`), while
consumers who inject the concrete `LoggingRestClient` type also get the entire RestSharp `RestClient`
surface (`ExecuteAsync`, etc.). Keep both injection paths working when changing the class.

**Keyed DI registration** (`ServiceCollectionExtensions.AddSeriRestClient` — the method name is
unchanged even though the type it lived on was renamed) is what enables multiple independently-configured
clients in one app:

- `RestClientOptions` → keyed **singleton**, built by the caller's factory.
- `ILoggingRestClient` → keyed **scoped**.
- `LoggingRestClient` (concrete) → keyed **scoped**, registered *separately*.

Because the interface and the concrete type are two registrations with two factories, resolving both
under the same key in one scope produces **two distinct client instances**. Anything that must be
shared per scope (cookie containers, connection state) will not be shared across them.

The extension also requires a **non-keyed `Serilog.ILogger`** in the container
(`sp.GetRequiredService<ILogger>()`), so `UseSerilog(...)` or an equivalent registration is a
precondition for resolution to succeed.

**Logging behavior** (`LoggingRestClient.LogRequest<T>`):

- The logger is enriched with `SourceContext = "REST: {BaseUrl}"` per client instance. Because that
  string contains a colon, no `appsettings.json` `Serilog:MinimumLevel:Override` key can ever match it —
  `Microsoft.Extensions.Configuration` splits on `:` before Serilog sees the key. Only the C#
  `MinimumLevel.Override(...)` form works. See the README's Configuration section for the full
  explanation and the working alternative.
- Levels: `Debug` for request/response detail and timing; `Warning` when the HTTP call completed but
  `IsSuccessful` is false; `Error` when the HTTP call itself failed.
- Bodies are pretty-printed — request bodies by `RestRequest.RequestFormat` (`DataFormat.Json` via
  Newtonsoft, `DataFormat.Xml` via `XmlSerializer`), response bodies by an **exact** match on
  `response.ContentType` against `application/json` / `application/xml`. A charset suffix
  (`application/json; charset=utf-8`) does not defeat the match — RestSharp strips media-type
  parameters before this check runs — but a `+json` structured-syntax subtype
  (`application/vnd.api+json`) falls through to the unformatted default.
- `LogRequest<T>` is **synchronous** (it calls `Execute<T>`, not `ExecuteAsync`).
- Everything is logged verbatim, including cookies and header values. There is no redaction.

## CI/CD (`.github/workflows/`)

- **`ci.yml`** — runs on push/PR: builds and tests on a 3-OS matrix (Ubuntu/Windows/macOS) across all
  three target frameworks, publishes MTP's TRX/cobertura output, and verifies packaging in a separate job.
- **`release.yml`** — runs on `v*` tags: build, test, pack, then publishes to nuget.org via Trusted
  Publishing (OIDC, no long-lived API key) and creates a GitHub Release. The filename and its
  `environment: nuget-release` value are both registered in the Trusted Publishing policy — do not
  rename or change either.

Both workflows use Microsoft.Testing.Platform's `dotnet test -- --report-trx --coverage
--coverage-output-format cobertura` invocation, not VSTest flags.

## Conventions

`.editorconfig` is authoritative: 4-space indent for C#, file-scoped namespaces, Allman braces
(newline before `{`, `else`, `catch`, `finally`), system usings sorted first. Nullable reference types
and implicit usings are enabled across all projects. Public members carry full XML doc comments
(`GenerateDocumentationFile` is on) — match that when adding public API. `TreatWarningsAsErrors` is on
repo-wide, so a missing doc comment on a public member is a hard build error.
