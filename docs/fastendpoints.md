# FastEndpoints

FastEndpoints v8.2 and later generate their OpenAPI documents through
`Microsoft.AspNetCore.OpenApi` (the `FastEndpoints.OpenApi` package), not through NSwag. That makes
this guide a short one: the [`OpenApiFeatureFlags.AspNetCore`](aspnetcore.md) adapter plugs into
FastEndpoints' own pipeline with a single line, its documented `ConfigureOpenApi` hook.

## Wiring

```csharp
using FastEndpoints;
using FastEndpoints.OpenApi;

var bld = WebApplication.CreateBuilder(args);

bld.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

bld.Services.AddFastEndpoints()
    .OpenApiDocument(o =>
    {
        o.Title = "My API";
        o.Version = "1.0";

        // Hand the built-in engine's options to this library. FastEndpoints runs these
        // transformers alongside its own.
        o.ConfigureOpenApi = options => options.AddOpenApiFeatureFlagTransformers();
    });

var app = bld.Build();

app.UseFastEndpoints();
app.MapOpenApi();   // serves /openapi/v1.json

app.Run();
```

Do not call `services.AddOpenApi()` directly: FastEndpoints warns that it bypasses its own
transformers and metadata handling, and `ConfigureOpenApi` is the supported route for exactly this
kind of addition.

This adapter targets `net10.0` (see [`aspnetcore.md`](aspnetcore.md) for why), so FastEndpoints must
be running on .NET 10; a net8.0 or net9.0 application should stay on FastEndpoints' older NSwag-based
stack or upgrade.

## How to gate an endpoint

**An operation** is gated from `Configure()`, with the attribute attached as endpoint metadata:

```csharp
public sealed class CheckoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/orders/checkout");
        AllowAnonymous();

        Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout")));
    }

    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync("checked out");
}
```

**A DTO property** is gated on the request or response class, exactly as a model property is
anywhere else; the property disappears from the schema while its flag is off:

```csharp
public sealed class CreateOrderRequest
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}
```

Schema names are FastEndpoints' own: the DTO's full name with the separators stripped
(`MyApp.CreateOrderRequest` becomes `MyAppCreateOrderRequest`) unless `ShortSchemaNames` is enabled.
Only the name changes; gating itself is unaffected.

**Part of a sentence** is gated with `<gate>` inside the strings `Summary()` sets; they reach the
document verbatim, so the plain form works and no escaping is needed (unlike NSwag):

```csharp
Summary(s =>
{
    s.Summary = "Search orders. <gate flag=\"InternalSearch\">Internal callers can pass scope.</gate>";
});
```

## What does not work, measured

Two boundaries were measured against FastEndpoints 8.3.0 and recorded here so they are not
rediscovered:

- **An `[OpenApiFeatureFlag]` on the endpoint class or on one of its methods does not gate
  anything.** FastEndpoints does not copy attributes on the endpoint type or its members into the
  ASP.NET Core endpoint metadata, and the adapter reads gating for non-controller endpoints from
  that metadata. Use `Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("X")))` in
  `Configure()` instead; the adapter's test suite locks both boundaries so a FastEndpoints change
  that alters them will be seen. The `OpenApiFeatureFlags.Analyzers` package reports this one at
  build time (OAFF5001).
- **A GET request DTO property cannot be gated.** FastEndpoints flattens GET DTO properties into
  query parameters, and a parameter can only be removed through the controller path (its
  `ParameterInfo`), which a FastEndpoints endpoint does not have. This is the same class of
  limitation as a minimal API's parameter under this engine. Gate the whole operation, or move the
  parameter behind a release the flag can hide as a unit.

## Post bodies, responses and descriptions

Everything that flows through the engine works as described in [`aspnetcore.md`](aspnetcore.md):
response schemas, request body schemas, and `<gate>` fragments all resolve, because the transformers
run at the engine level and FastEndpoints only feeds them.

## Testing

`tests/OpenApiFeatureFlags.FastEndpoints.Tests` requests `/openapi/v1.json` from a real host through
`Microsoft.AspNetCore.TestHost`, with FastEndpoints registered exactly as above. It runs on
`net10.0`.
