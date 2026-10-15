# Analyzers

`OpenApiFeatureFlags.Analyzers` is a build-time companion to the family. It reports, while the
project compiles, the mistakes that otherwise fail **silently** at document time: a gate that hides
its content forever, a fragment that leaks while its flag is off, an attribute that reaches no
document at all. It ships no runtime code and has no dependencies; the package carries the analyzer
assembly under `analyzers/dotnet/cs`, and it is versioned together with the rest of the family.

```
dotnet add package OpenApiFeatureFlags.Analyzers
```

Install it next to the adapter package a project already uses.

## Diagnostic IDs

An ID is `OAFF` plus four digits, and the leading digit names the area the finding belongs to, so
an ID alone says where to look:

| Block | Area |
| --- | --- |
| `OAFF1xxx` | Core gating rules, every document engine |
| `OAFF2xxx` | The built-in `Microsoft.AspNetCore.OpenApi` engine |
| `OAFF3xxx` | NSwag |
| `OAFF4xxx` | Swashbuckle (reserved, no rules yet) |
| `OAFF5xxx` | FastEndpoints |
| `OAFF6xxx` | Flag sources: FeatureManagement, OpenFeature (reserved, no rules yet) |

## The rules

### OAFF1001, gate fragment without a flag attribute

Applies to every engine except NSwag. A `<gate>` element with no `flag` attribute is fail-closed on
the engines that read documentation comments as written: its content is hidden in every flag state,
usually because a flag name was forgotten.

```csharp
/// <summary>
/// The total. <gate>was the flag forgotten here?</gate>
/// </summary>
public decimal Total { get; set; }   // OAFF1001: add flag="...", or remove the fragment
```

Under NSwag the same fragment is dropped instead of hidden, so OAFF3001 speaks there.

### OAFF1002, gated elements with no adapter package referenced

Applies to every engine. The attribute carries no behaviour by itself: a project that references a
document engine but no adapter package compiles cleanly and every gated element stays in the
document. A project that references no engine at all is left alone, because declaring the attribute
in a contracts library is legitimate and the adapter belongs to the host.

```csharp
public sealed class OrdersController
{
    [OpenApiFeatureFlag("NewCheckout")]        // OAFF1002: add OpenApiFeatureFlags.Swashbuckle,
    public IActionResult Checkout() => Ok();   // .AspNetCore or .NSwag, and register it
}
```

One boundary: if an adapter for a *different* engine is referenced (the built-in engine in use, the
NSwag adapter installed), the rule stays quiet. A mixed or migrating setup is legitimate, and a
build-time check cannot see a registration call that lives in another project.

### OAFF2001, minimal API parameters cannot be gated by the built-in engine

Applies when the built-in engine's adapter (`OpenApiFeatureFlags.AspNetCore`) is referenced and the
NSwag adapter is not. The engine ignores `[OpenApiFeatureFlag]` on a minimal API delegate's
parameter ([why](aspnetcore.md)); the attribute never reaches the document.

```csharp
app.MapGet("/search", ([OpenApiFeatureFlag("InternalSearch")] string? scope) => "results");
// OAFF2001: gate the operation with .WithMetadata(new OpenApiFeatureFlagAttribute("...")),
// or take the NSwag adapter, which can gate a minimal API's parameter
```

### OAFF3001, escape gate fragments in XML documentation comments (NSwag)

Applies when the NSwag adapter is referenced. NSwag's documentation reader drops the literal
`<gate>` tag, so the fragment is not gated and its content stays visible while the flag is off.
Write it escaped; the escaped form is decoded to the gate text the other engines produce and
resolves correctly ([why](nswag.md)).

```csharp
/// <summary>
/// The total. &lt;gate flag="Beta"&gt;Beta pricing applies.&lt;/gate&gt;
/// </summary>
public decimal Total { get; set; }
```

### OAFF5001, attribute on a FastEndpoints endpoint does not gate it

Applies to FastEndpoints endpoints. FastEndpoints does not carry attributes on the endpoint class
or its members into endpoint metadata, so `[OpenApiFeatureFlag]` placed on one never gates anything
([why](fastendpoints.md)).

```csharp
[OpenApiFeatureFlag("NewCheckout")]
public sealed class CheckoutEndpoint : EndpointWithoutRequest   // OAFF5001
{
    public override void Configure()
    {
        Post("/orders/checkout");
        Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout")));   // do this
    }
}
```

## Severity and suppression

Every rule ships as a warning. Raise one to an error, or silence it, per project in
`.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.OAFF3001.severity = error
dotnet_diagnostic.OAFF1001.severity = none
```

A suppressions file works the same way (`#pragma warning disable OAFF5001`, or a
`<NoWarn>OAFF5001</NoWarn>` entry).
