# Trying the packages locally

The released packages are on [nuget.org](https://www.nuget.org/packages/OpenApiFeatureFlags), and that
is how they are normally consumed. This page covers the other case: building from source to try a
change that has not been released yet.

`local-packages/` is a local NuGet feed containing the built packages.

```shell
./scripts/pack-local.ps1
```

That packs every package in Release configuration, clears the folder first, and prints the
`nuget.config` you need. There is also a `-VersionSuffix` switch, explained at the bottom of this page.

It is equivalent to packing each package project:

```shell
for project in src/*/*.csproj; do dotnet pack "$project" -c Release -o local-packages; done
```

| Package | Version |
|---|---|
| `OpenApiFeatureFlags` | 0.1.1 |
| `OpenApiFeatureFlags.Abstractions` | 0.1.1 |
| `OpenApiFeatureFlags.Swashbuckle` | 0.1.1 |
| `OpenApiFeatureFlags.AspNetCore` | 0.1.1 |
| `OpenApiFeatureFlags.FeatureManagement` | 0.1.1 |
| `OpenApiFeatureFlags.OpenFeature` | 0.1.1 |

Every package moves together, at the `VersionPrefix` in `Directory.Build.props`. See [`CHANGELOG.md`](../CHANGELOG.md)
for what changed in this cut. `OpenApiFeatureFlags.AspNetCore` targets `net10.0` only, so it does not
appear in a `net8.0` or `net9.0` output path.

`local-packages/` is gitignored — it is build output, not source.

## Point a project at the feed

Create `nuget.config` next to the consuming solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="C:\Projects\github\dogukandemir\OpenApiFeatureFlags\local-packages" />
    <!-- Keep nuget.org: the packages' own dependencies live there. -->
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Or add it to your user-level config, which affects every project on the machine:

```shell
dotnet nuget add source "C:\Projects\github\dogukandemir\OpenApiFeatureFlags\local-packages" --name oaff-local
```

**Keep nuget.org enabled.** The `local` feed only carries these five packages; `Swashbuckle.AspNetCore`,
`Microsoft.OpenApi`, `Microsoft.FeatureManagement` and `OpenFeature` are restored from nuget.org. A feed
list containing only `local` will fail to restore.

If your corporation uses `packageSourceMapping`, add a pattern for `OpenApiFeatureFlags.*`; the packages
also need their own dependencies to resolve, which they do through the upstream feed.

## Minimal project that works

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.3" />
    <PackageReference Include="OpenApiFeatureFlags.Swashbuckle" Version="0.1.1" />
    <PackageReference Include="OpenApiFeatureFlags.FeatureManagement" Version="0.1.1" />
  </ItemGroup>
</Project>
```

```csharp
using Microsoft.OpenApi;
using OpenApiFeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Registers Microsoft.FeatureManagement and points the library at IFeatureManager.
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Consumer API", Version = "1.0" });

    // Registered last. See "Filter ordering" in the README.
    options.AddOpenApiFeatureFlagFilters();
});

var app = builder.Build();
app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI();
app.Run();
```

```csharp
using Microsoft.AspNetCore.Mvc;
using OpenApiFeatureFlags;   // the attribute's namespace

namespace Consumer;

public sealed class Widget
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}

[ApiController]
[Route("api/widgets")]
public sealed class WidgetsController : ControllerBase
{
    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();
}
```

```json
{
  "FeatureManagement": {
    "NewCheckout": false,
    "LoyaltyProgram": false
  }
}
```

Note `using OpenApiFeatureFlags;` — the attribute lives in namespace `OpenApiFeatureFlags`, not in a
namespace named after the package. The `OpenApiFeatureFlags.Swashbuckle` package brings it in
transitively through `*.Abstractions`.

## Check it without running a server

Add these two properties and the document is written during `dotnet build`:

```xml
<OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)\openapi</OpenApiDocumentsDirectory>
```

The document lands in `openapi/<AssemblyName>.json`. This path is worth exercising: it builds the host
with **no `HttpContext`**, which is the offline code path, and it is how CI exports a document. Forcing
a full rebuild re-runs it:

```shell
dotnet build -c Release --no-incremental
```

Flip a flag in `appsettings.json`, rebuild, and watch the element appear or disappear.

## Rebuilding after a code change

```shell
dotnet pack -c Release -o local-packages
```

NuGet caches by version, so a consumer that already restored that version may keep using the old copy
until you clear the cache:

```shell
dotnet nuget locals global-packages --clear     # blunt; or delete the entry under %USERPROFILE%\.nuget\packages\openapifeatureflags
```

The consumer's own `obj/project.assets.json` caching is the more common culprit — `dotnet restore
--force` on the consumer, or delete its `obj` and `bin` folders.

## Avoiding a clash with the published release

The version in `VersionPrefix` is normally published already, so a local build at that same version
can be shadowed by the published one depending on source order. For a throwaway trial you can build a
distinctly-versioned set instead:

```shell
./scripts/pack-local.ps1 -VersionSuffix local
```

That produces `0.1.1-local`, which cannot be confused with a published release. Bumping
`VersionPrefix` is the other way out, and is what a new feature would do anyway.
