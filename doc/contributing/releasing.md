# Releasing

## Versioning

We follow [Semantic Versioning].
The version X.Y.Z indicates:

* X is the major version (backward-incompatible changes to the API, the supported target frameworks or the major version of a runtime dependency),
* Y is the minor version (backward-compatible new features), and
* Z is the patch version (backward-compatible bug fixes).

[Semantic Versioning]: http://semver.org/spec/v1.0.0.html 

## Release to NuGet

To release a new version, publish a [GitHub release] on `main` with the tag `vX.Y.Z` (*e.g.*, `v2.0.0`).

Publishing the release triggers the workflow `publish-to-nuget.yml` in [`.github/workflows`].
The workflow takes the package version from the tag, builds the package and pushes it to NuGet using [trusted publishing], so no API key is needed.
The `<Version>` in `AasCore.Aas3.Package.csproj` is only used for local builds.

[GitHub release]: https://github.com/eclipse-aascw/aas-package3-csharp/releases/new
[`.github/workflows`]: https://github.com/eclipse-aascw/aas-package3-csharp/tree/main/.github/workflows
[trusted publishing]: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing

## Building the Package Locally

To build the package locally, invoke:

```powershell
dotnet pack -c Release -o artefacts src/AasCore.Aas3.Package
```

In cases of substantial changes to the solution, you need to delete `bin` and `obj` subdirectories beneath `src` as dotnet will not do that for you.
We provide a shallow script to save you a couple of keystrokes:

```powershell
.\src\RemoveBinAndObj.ps1
```
