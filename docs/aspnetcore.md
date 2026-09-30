# The ASP.NET Core adapter

`OpenApiFeatureFlags.AspNetCore` gates the document produced by the built-in
`Microsoft.AspNetCore.OpenApi` document engine — the one behind `builder.Services.AddOpenApi()` and
`app.MapOpenApi()` — instead of Swashbuckle.

Everything the library promises is unchanged: gated operations, parameters and model properties
disappear from the document when their flag is off, `Annotate` marks them instead of hiding them, and
`<gate>` works inside descriptions exactly as it does under Swashbuckle. What differs is how the
adapter attaches to the engine, and one hard constraint.

## Wiring

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// The core services, plus a flag source. The one-call helper is the shortest path.
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

builder.Services.AddOpenApi(options =>
{
    options.AddOpenApiFeatureFlagTransformers();
});

var app = builder.Build();

app.MapOpenApi();   // serves /openapi/v1.json
```

`AddOpenApiFeatureFlagTransformers()` registers three transformers, and the order between them does
not matter: the engine runs schema transformers first, then operation transformers, then document
transformers, whatever order they were added in.

It is the direct counterpart of `AddOpenApiFeatureFlagFilters()` in the Swashbuckle adapter, so
switching engines changes one line at the registration site and nothing else.

> Register it **after** any of your own document transformers that rebuild `paths` or `tags`. If yours
> runs last, it will put back what this library removed.

## Two ways an endpoint gets gated

A controller is read through its `MethodInfo`, so an attribute on the controller class, on the action
or on a parameter all work exactly as they do under Swashbuckle:

```csharp
[OpenApiFeatureFlag("NewCheckout")]
[HttpPost("checkout")]
public IActionResult Checkout() => Ok();
```

A minimal API has no `MethodInfo`, so the attribute is read from the endpoint's metadata instead:

```csharp
app.MapGet("/orders/checkout", () => "checkout")
   .WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout"));
```

Both work. The second path exists because otherwise a minimal API could not be gated at all, which is
not what `[OpenApiFeatureFlag]` promises.

## What is not supported

**A parameter of a minimal API cannot be gated.** A controller parameter is removed by looking its
`ParameterInfo` up in the action's `MethodInfo`, and a minimal API's metadata carries no such thing, so
there is nothing to match on. Gating the *operation* works; gating one of its parameters there does
not, and this is recorded here rather than left to be discovered.

## `net10.0` only, and why

This adapter targets `net10.0` and no earlier framework. That is a cost, not an oversight, and the
reason is a version wall rather than an API one:

| Engine | Its `Microsoft.OpenApi` dependency | Document model |
|---|---|---|
| `Swashbuckle.AspNetCore.Swagger` 10.2.3 | `2.7.5` (exact) | 2.x |
| `Microsoft.AspNetCore.OpenApi` 10.0.x | `[2.12.0, 3.0.0)` | 2.x |
| `Microsoft.AspNetCore.OpenApi` 9.0.x | `1.6.17` | 1.x |
| `Microsoft.AspNetCore.OpenApi` 8.0.x | `1.4.3` | 1.x |

Two consequences, both measured rather than reasoned about:

- The 8.x and 9.x lines sit on the **1.x** model (`Microsoft.OpenApi.Models.*`) while 10.x sits on the
  2.x model, so this is not one source file across three target frameworks — it would be three
  implementations. One of them is shipped.
- 10.x floors `Microsoft.OpenApi` at **2.12.0** and Swashbuckle 10.2.3 requires exactly **2.7.5**, so
  **a Swashbuckle adapter and this adapter cannot be used in the same application.** Pick one document
  engine per application. They can coexist in one *solution*: the two engines resolve their own
  versions in their own projects.

If you are on .NET 8 or .NET 9, use `OpenApiFeatureFlags.Swashbuckle`; the 9.x engine's document model
is a different set of types and this adapter does not build against it.

## XML comments need one extra line

If your project emits XML documentation — `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
plus doc comments — and references `Microsoft.AspNetCore.OpenApi`, the build fails with:

```
error CS9137: The 'interceptors' feature is not enabled in this namespace.
```

The fix is a property in the project that documents the endpoints:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
</PropertyGroup>
```

**This is a requirement of the engine, not of this library.** The engine's XML-comment support is a
source generator that emits interceptors, and .NET refuses to compile an interceptor unless its
namespace is opted into. Verified both ways: a project that references the engine and emits no XML
documentation builds without the line, and the same project fails the moment it has doc comments.

It matters here because `<gate>` is written inside XML doc comments, so a project using description
gating has XML documentation enabled by definition.

## Differences you may notice

- **Property gating is more direct.** Under Swashbuckle a gated property is tagged and resolved later
  by object identity, because the schema filter never sees the parent. Here the transformer is handed
  the JSON type information, so the document's property name and the member the attribute was written
  on come from the same source and no naming rules are reproduced.
- **`required` is cleaned twice.** Once as each property is removed, and once more in the final pass
  over the document, because a consumer schema transformer registered after this one could add an entry
  back for a property that is already gone.
- **Component schemas are collected after the transformers run.** The engine moves schemas into
  `components.schemas` only once every transformer has finished, so this adapter walks request and
  response bodies as well — a walk over `components.schemas` alone would miss a schema that is still
  inline.

## Testing

Unlike Swashbuckle there is no `ISwaggerProvider` to resolve, so the only public way to obtain a
document is to ask the endpoint that serves it. `tests/OpenApiFeatureFlags.AspNetCore.Tests` does this
through `Microsoft.AspNetCore.TestHost`, which is also the position the transformers really run in.

One thing that host makes visible: an exception thrown while generating the document — the canary, for
instance — surfaces to the caller as the exception itself rather than as a 500 response. A deployed
server turns the same exception into a 500.
