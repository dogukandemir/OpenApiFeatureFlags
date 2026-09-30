# Modes

`DocumentMode` decides what a gated element looks like in the published document.

```csharp
builder.Services.AddOpenApiFeatureFlags(options =>
{
    options.Mode = DocumentMode.Remove;   // the default
});
```

Every sample below is what the library actually produces; the Swashbuckle test suite asserts them.

A `<gate>` in a description is a partial exception: its *wrapper* is always stripped, in every mode,
because the markup is the library's own invention and must never reach a consumer. Only the content
is gated. See [`descriptions.md`](descriptions.md).

---

## `Remove` — the default

The gated element is not in the document at all. This is the mode that fixes the original problem: a
customer reading the document can no longer see surface they cannot call.

Given:

```csharp
public sealed class Order
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }

    public string? Note { get; set; }
}

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    [HttpPost]
    public IActionResult Create(Order order) => Ok(order);
}
```

With `NewCheckout` and `LoyaltyProgram` both off:

```json
{
  "paths": {
    "/api/orders": { "post": { "responses": { "200": { "description": "OK" } } } }
  },
  "components": {
    "schemas": {
      "Order": {
        "type": "object",
        "properties": {
          "id": { "type": "string", "nullable": true },
          "note": { "type": "string", "nullable": true }
        }
      }
    }
  }
}
```

Note what else happened, because these are the parts that are easy to get wrong:

- `POST /api/orders/checkout` is gone, and so is the path item — an empty path is still published
  surface.
- `loyaltyPoints` is gone from `Order`, but its siblings survived.
- If `loyaltyPoints` had been in `required`, its name would have been removed from `required` too.
  A dangling `required` entry makes client generators emit code for a property that does not exist.
- If the only reference to a component schema was a gated property, that schema is removed as well.

### With `Annotate` in the same document

Because the decision is per document, one API can publish a `Remove` document for customers and an
`Annotate` document for an internal portal by registering two `SwaggerDoc` documents with different
configurations.

---

## `Annotate`

Nothing is removed. Every gated **operation** gains an `x-feature-flag` extension listing the flags
that govern it, and the document gains one at the root listing every flag involved.

```csharp
options.Mode = DocumentMode.Annotate;
```

```json
{
  "x-feature-flag": ["LoyaltyProgram", "NewCheckout"],
  "paths": {
    "/api/orders/checkout": {
      "post": {
        "x-feature-flag": ["NewCheckout"],
        "responses": { "200": { "description": "OK" } }
      }
    }
  }
}
```

This mode is for portals that want to filter non-destructively, and for teams who want to see which
surface *would* disappear before they turn `Remove` on.

Two things it does that are worth knowing:

- It never reads the flag source. Annotation needs the flag *names*, not their state, so an
  unreachable store cannot damage an annotated document.
- Nothing is removed, so the flag source cannot leak anything either. `Annotate` is the safe mode to
  start with.

> The exact shape of the extensions is still open — see [`design.md`](design.md).
> Today it is an array of flag names, on gated operations and at the root. A gated **schema property**
> is left unmarked, so a client can tell that a flag governs *something* in the document but not which
> property it gates; the root array is where that flag name still appears.

---

## `Include`

The library does nothing. Every flag is ignored and the full document is published.

```csharp
options.Mode = DocumentMode.Include;
```

This is the per-environment off switch: useful when generating reference documentation for an
internal audience where every endpoint should be visible, or as a blast-radius limiter while you
roll the library out.

Like `Annotate`, it never reads the flag source, and the document is byte-identical to one generated
without the library installed.

---

## Choosing

| You want | Use |
|---|---|
| Customers to stop seeing unreleased surface | `Remove` |
| A portal that filters client-side, or a dry run before switching to `Remove` | `Annotate` |
| Everything visible, in one environment | `Include` |
