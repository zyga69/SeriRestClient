# Contributing

Thanks for taking the time to contribute.

## Getting set up

Install the .NET SDK version pinned in `global.json`, then:

```
dotnet restore
dotnet build
dotnet test
```

## Pull requests

- For anything beyond a small fix, open an issue first so we can agree on the
  approach before you spend time on it.
- Add or update tests for behaviour changes.
- Keep CI green; the build treats warnings as errors.
- Note any change to the public API in the PR description — it affects
  versioning.

## Versioning and releases

This project follows [Semantic Versioning](https://semver.org). Versions are
derived from git tags by MinVer, so a release is cut by pushing a tag:

```
git tag v1.2.3
git push origin v1.2.3
```

Maintainers do this; contributors do not need to touch version numbers
anywhere in the source.
