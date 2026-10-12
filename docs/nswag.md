# The NSwag adapter

`OpenApiFeatureFlags.NSwag` gates the document produced by NSwag's ASP.NET Core generator (the one
behind `builder.Services.AddOpenApiDocument()` and `app.UseOpenApi()`) instead of Swashbuckle.

Everything the library promises is unchanged: gated operations, parameters and model properties
disappear from the document when their flag is off, `Annotate` marks them instead of hiding them, and
`<gate>` works inside descriptions exactly as it does under Swashbuckle. What differs is how the
adapter attaches, and two capabilities this engine makes possible that the built-in one does not.

## Wiring

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// The core services, plus a flag source. The one-call helper is the shortest path.
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

builder.Services.AddOpenApiDocument((settings, services) =>
    settings.AddOpenApiFeatureFlagProcessors(services));

var app = builder.Build();

app.UseOpenApi();   // serves /swagger/v1/swagger.json
```

Use the **two-argument** `AddOpenApiDocument` overload. NSwag builds its settings object inside a
singleton factory, and that factory is handed the application's `IServiceProvider`; this is where the
processors receive the shared planner and the logger factory. The single-argument overload cannot
reach a provider, so it cannot be used. NSwag offers the two-argument overload for exactly this kind
of dependency-aware configuration.

Call `AddOpenApiFeatureFlagProcessors()` **after** any of your own configuration that rebuilds
`paths`, `tags` or `components.schemas`. This mirrors the ordering guidance of the other adapters.

Both `AddOpenApiDocument` (OpenAPI 3) and `AddSwaggerDocument` (Swagger 2) work: the orphan sweep
reads the serialised document and recognises both reference shapes.

## Supported frameworks

`NSwag.Generation.AspNetCore` 14.7.1 ships `net8.0`, `net9.0` and `net10.0` targets on the
NJsonSchema 11.6.1 line, so unlike `OpenApiFeatureFlags.AspNetCore` (which is `net10.0` only), this
package targets `net8.0`, `net9.0` and `net10.0`. If you are on .NET 8 or .NET 9 and you use NSwag,
this is the adapter for you.

## Two ways an endpoint gets gated

A controller is read through its action `MethodInfo`, so an attribute on the controller class, on the
action or on a parameter works exactly as it does under Swashbuckle.

A minimal API is covered through two sources that are merged. NSwag surfaces the delegate through
endpoint metadata as a `MethodInfo`, so an attribute on the delegate itself is read directly:

```csharp
app.MapGet("/orders/checkout", [OpenApiFeatureFlag("NewCheckout")] () => "checkout");
```

and a `.WithMetadata(...)` registration, which never appears on a `MethodInfo`, is found by a
metadata scan:

```csharp
app.MapGet("/orders/checkout", () => "checkout")
   .WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout"));
```

A flag that appears only on a parameter is excluded from the operation set, so a gated parameter
never hides its whole operation.

## A minimal API's parameter can be gated here

This is the difference from the built-in engine adapter, and it comes from the measurement: NSwag
hands the operation processor a map from `ParameterInfo` to the generated parameter, and a minimal
API's delegate is present as a `MethodInfo`, so a gated minimal API parameter is removed by the same
route a controller parameter is.

```csharp
app.MapGet("/search", ([OpenApiFeatureFlag("SearchEnabled")] string? term) => "results");
```

Under `Microsoft.AspNetCore.OpenApi` this is not possible, because its endpoint metadata carries no
`ParameterInfo` to match on; that limitation is recorded in [`aspnetcore.md`](aspnetcore.md).

## How the processors work

Three measured facts shape the implementation (recorded in [`design.md`](design.md), section 6 under
"Built since"):

- An operation processor that returns `false` keeps the operation out of the document entirely, so
  operations are gated in one pass. No marker and no second visitor are needed, unlike the other two
  adapters.
- A schema processor returns nothing and mutates the schema it is given, so a gated property is
  removed from `schema.Properties`, and its name from `required`, on the spot.
- The document's name for a property is resolved with the generator's own reflection service
  (`GetPropertyName`), so camel-casing, `[JsonPropertyName]` and every other naming policy are
  identical on both sides of the match by construction.

The document processor resolves `<gate>` fragments in the document description, tag descriptions,
path item summaries and descriptions, operation summaries and descriptions, parameter, request body
and response descriptions, and the description of every reachable schema. It then sweeps `required`
once more after every processor has run (a consumer processor registered later can add an entry back
for a property that is already gone), and applies the plan:

- in `Remove`, gated operations are already gone, and a model referenced only by a removed operation
  is pruned from the schema collection, together with orphan tags;
- in `Annotate`, gated operations and the document root carry `x-feature-flag`.

## Differences you may notice

- **A path whose every operation was gated is not serialised at all.** NSwag writes a path entry only
  when it receives an operation for it, and a serialised empty `paths` object is omitted entirely.
- **The reference shape follows the document type.** An OpenAPI 3 document references model schemas
  as `#/components/schemas/...`, a Swagger 2 document as `#/definitions/...`; the orphan sweep checks
  both, so either registration style behaves the same.
- **Gates in XML documentation comments must be written escaped.** NSwag reads operation summaries and
  descriptions from documentation comments through a reader that flattens element markup, so a literal
  `<gate>` tag is dropped from the text and its content stays visible in every flag state. Writing the
  fragment as `&lt;gate flag="X"&gt;...&lt;/gate&gt;` makes it arrive as plain text, where it resolves
  like anywhere else; the suite holds both the off and the on direction. NSwag also does not lift a
  minimal API's `WithSummary`/`WithDescription` metadata into the document at all, so author gated
  operation prose through an escaped documentation comment rather than through those helpers.
- **`required` is cleaned twice**, once as each property is removed and once in the final sweep over
  the document. Same reasoning as in [`aspnetcore.md`](aspnetcore.md).

## Testing

The test project requests `/swagger/v1/swagger.json` from a real host through
`Microsoft.AspNetCore.TestHost`, which runs the processors in the position they occupy in a deployed
application, inside NSwag's generation pipeline. The suite runs on all three target frameworks.

One thing that host makes visible: an exception thrown while generating the document (the canary, for
instance) surfaces to the caller as the exception itself rather than as a 500 response. A deployed
server turns the same exception into a 500.
