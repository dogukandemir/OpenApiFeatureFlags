# OpenApiFeatureFlags.FastEndpoints.Sample

A runnable FastEndpoints API whose OpenAPI document is generated through
`Microsoft.AspNetCore.OpenApi` (the `FastEndpoints.OpenApi` package) and respects feature flags. It
is the FastEndpoints counterpart of [`OpenApiFeatureFlags.AspNetCore.Sample`](../OpenApiFeatureFlags.AspNetCore.Sample),
and it is the setup [`docs/fastendpoints.md`](../../docs/fastendpoints.md) describes.

## Run it

```shell
dotnet run --project samples/OpenApiFeatureFlags.FastEndpoints.Sample --urls http://localhost:5184
```

- Raw document: <http://localhost:5184/openapi/v1.json>

## What to look at

The flags live in `appsettings.json` under `FeatureManagement`. With the defaults (all off), the
document is missing everything the flags gate:

- `POST /api/orders/checkout` (`NewCheckout`), gated through
  `Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout")))`
- the `loyaltyPoints` property of `CreateOrderRequest`, and one sentence of the create-order
  description (`LoyaltyProgram`)

Flip a flag and request the document again, without editing the file:

```shell
FeatureManagement__NewCheckout=true dotnet run --project samples/OpenApiFeatureFlags.FastEndpoints.Sample --urls http://localhost:5184
```

## The two boundaries this sample shows

An `[OpenApiFeatureFlag]` on the endpoint class or on its members never reaches endpoint metadata, so
it never gates anything; the analyzer reports it as `OAFF5001`, and the supported route is the
`WithMetadata` call above. And a GET DTO property becomes a query parameter that cannot be gated
individually, so this sample gates a property of the **request body** instead.

Both boundaries were measured, are locked by tests, and are written up in
[`docs/fastendpoints.md`](../../docs/fastendpoints.md).
