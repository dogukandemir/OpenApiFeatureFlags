# OpenApiFeatureFlags

Feature flags for OpenAPI documents.

Mark the endpoint, action, property or parameter with `[OpenApiFeatureFlag]` and it only appears in
the generated document once the flag is enabled — using the same flags the runtime already reads.

```csharp
[OpenApiFeatureFlag("NewCheckout")]
[HttpPost("checkout")]
public IActionResult Checkout() => Ok();
```

With `NewCheckout` off, `/api/orders/checkout` is **not** in `swagger.json`. With it on, it is. No
restart, no rebuild, no conditional compilation.

---

## The problem this solves

A document is generated from the code, so it describes every endpoint the code declares. A feature
toggle only gates *runtime* behaviour. The result is customers reading documented API surface they
cannot use — and, once the document is published, a support conversation about an endpoint that
"exists" but does not work.

## Installation

| Package | What it is for |
|---|---|
| `OpenApiFeatureFlags` | Core: the planner and the service registration. |
| `OpenApiFeatureFlags.Swashbuckle` | Swashbuckle filter set. Add this if you use `AddSwaggerGen`. |
| `OpenApiFeatureFlags.FeatureManagement` | Reads flags from Microsoft's `IFeatureManager`. |
| `OpenApiFeatureFlags.OpenFeature` | Reads flags through the CNCF [OpenFeature](https://openfeature.dev) standard. |
| `OpenApiFeatureFlags.Abstractions` | Attribute and contracts only. Reference this from assemblies that must not take a dependency on Swashbuckle or a flag library. |

```shell
dotnet add package OpenApiFeatureFlags.Swashbuckle
dotnet add package OpenApiFeatureFlags.FeatureManagement
```

> Not published yet. To try it today, build the packages into the local feed and add it as a source —
> see [`docs/trying-locally.md`](docs/trying-locally.md).

## Quickstart

### 1. Register the services

```csharp
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "1.0" });

    // Register this LAST. See "Filter ordering" below.
    options.AddOpenApiFeatureFlagFilters();
});
```

`AddOpenApiFeatureFlagsWithFeatureManagement()` is one call that registers your existing
`Microsoft.FeatureManagement` setup *and* points OpenApiFeatureFlags at it. Already using feature
management? Chain instead:

```csharp
builder.Services
    .AddOpenApiFeatureFlags(options => options.Mode = DocumentMode.Annotate)
    .UseFeatureManagement();
```

Bringing your own flag store? Implement one interface and register it:

```csharp
public sealed class MyFlagSource(IMyToggleStore store) : IFeatureFlagSource
{
    public bool IsEnabled(string flagName) => store.IsOn(flagName);
}

builder.Services.AddOpenApiFeatureFlags(new MyFlagSource(store));
```

**Azure App Configuration needs no adapter of its own.** It is a configuration source that feeds
`Microsoft.FeatureManagement`, and `OpenApiFeatureFlags.FeatureManagement` reads through
`IFeatureManager`, so the wiring above is the whole integration — plus the three lines that add the
store. See [`docs/azure-app-configuration.md`](docs/azure-app-configuration.md).

### 2. Mark what is gated

```csharp
[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public Order Get(string id) => _orders.Find(id);

    [OpenApiFeatureFlag("NewCheckout")]                  // gates this operation
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    [HttpGet("search")]
    public IActionResult Search(
        [OpenApiFeatureFlag("InternalSearch")] string? scope) => Ok();   // gates one parameter
}

public sealed class Order
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]               // gates one response property
    public int LoyaltyPoints { get; set; }
}
```

The attribute works on a controller, an action, a model property or a parameter. Two attributes on
one target are **AND**ed: the element is documented only when every flag is enabled.

### 3. Done

Nothing else. The document is regenerated on every request, so flipping a flag changes the published
document without a restart.

## Modes

| Mode | Behaviour |
|---|---|
| `Remove` (default) | Gated elements are removed from the document. |
| `Annotate` | Gated elements stay, tagged with `x-feature-flag`, so a portal can filter them. |
| `Include` | The library does nothing; the full document is published. |

See [`docs/modes.md`](docs/modes.md) for output samples.

## What this library does not do

**It never changes runtime behaviour.** Hiding an operation from the document leaves the endpoint
routeable; hiding a property leaves it serialised. A documentation decision must not silently become
a behaviour change, and this is the one rule the design refuses to bend — see decision D2 in
[`BACKLOG.md`](BACKLOG.md). Runtime gating stays in your application code.

## Guarantees worth knowing

- **Fail closed.** If a flag cannot be resolved, the element is hidden rather than leaked.
- **Fail loudly instead of quietly deleting.** Failing closed is only safe while your flag store
  answers. If a document contains gated elements and *no* flag could be read, the library throws
  `FeatureFlagSourceUnavailableException` rather than publishing a document that is silently missing
  released endpoints. Turn it off with `CanaryEnabled = false` if you accept that risk.
- **Invisible when unused.** With no attributes applied, the produced document is byte-identical to
  one generated without the library. There is a regression test that asserts exactly this.

## Filter ordering

Swashbuckle applies filters in registration order, and this matters:

- Call `AddOpenApiFeatureFlagFilters()` **after** your own filters and document processors. A
  processor that clears and rebuilds `Paths` would otherwise put removed operations back, and one
  that rebuilds `Tags` would put removed tags back.

The registration order inside `AddOpenApiFeatureFlagFilters` is deliberate and documented in the
method's XML docs.

## Documentation

- [`docs/trying-locally.md`](docs/trying-locally.md) — build the packages and consume them from a local
  feed, with a minimal project that works.
- [`docs/modes.md`](docs/modes.md) — what each mode produces.
- [`docs/azure-app-configuration.md`](docs/azure-app-configuration.md) — reading flags from Azure App
  Configuration, and the four ways that can go quietly wrong.
- [`docs/troubleshooting.md`](docs/troubleshooting.md) — nothing is hidden, or too much is.
- [`samples/OpenApiFeatureFlags.Sample`](samples/OpenApiFeatureFlags.Sample) — a runnable API.
- [`BACKLOG.md`](BACKLOG.md) — the design decisions, the measured facts and the remaining work.

## Author

**Dogukan Demir** — [@dogukandemir](https://github.com/dogukandemir)

## Licence

MIT.

