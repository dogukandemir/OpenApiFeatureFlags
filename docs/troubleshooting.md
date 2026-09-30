# Troubleshooting

Turn on `Debug` logging for the `OpenApiFeatureFlags` category: every document generation writes one
line saying what it hid and which flags it evaluated.

```
OpenApiFeatureFlags finished the document in mode Remove: 3 gated element(s) hidden, 5 kept, flags involved LoyaltyProgram, NewCheckout.
OpenApiFeatureFlags: no gated elements in this document (mode Remove).
```

---

## Nothing is hidden

Work down this list; each step answers a different question.

**1. Is the filter registered at all?**

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "1.0" });
    options.AddOpenApiFeatureFlagFilters();   // <- this
});
```

**2. Is it registered *last*?**

Filters run in registration order. If you registered a document processor after
`AddOpenApiFeatureFlagFilters()`, and that processor clears and rebuilds `Paths`, it will put removed
operations back. Move the call to the end.

**3. Is the mode `Remove`?**

`Include` publishes everything by design and `Annotate` removes nothing. Check the `Mode` line in the
log.

**4. Did the flag actually resolve?**

Look for this warning:

```
OpenApiFeatureFlags could not evaluate the flag {FlagName}; treating it as disabled so the document fails closed.
```

If it is there, the element *was* hidden — the flag source threw. Note that fail-closed means the
element stayed hidden, which is the intended behaviour.

**5. Is the attribute on the thing you think it is?**

- On a **controller**: gates every action on it.
- On an **action**: gates that operation.
- On a **parameter**: gates that parameter only — it does not hide the operation.
- On a **property**: gates that schema property.

The attribute is **not inherited**. Putting it on a base controller does not gate the derived
controller's own actions. If the action itself is declared on the base class, the base class's
controller-level attributes do apply, because that is where the action actually lives.

**6. Is the schema property you expect actually gated?**

`[OpenApiFeatureFlag]` is read from the property. If the property is serialised under a different
name, or replaced by a custom converter, the attribute still has to be on the CLR member.

**7. Is the flag store actually answering?**

With Azure App Configuration this is the usual cause: `AddAzureAppConfiguration(...)` without
`UseFeatureFlags()`, or a `Label` that does not match the environment, selects no flags at all — and
"the flag is missing" is indistinguishable from "the flag is off" to a fail-closed library. See
[`azure-app-configuration.md`](azure-app-configuration.md).

**8. Zero flags resolved at all?**

```
OpenApiFeatureFlags.FeatureFlagSourceUnavailableException
```

This is deliberate. If a document has gated elements and *not one* flag could be read, publishing
would silently drop released endpoints. Fix the flag source, or set `CanaryEnabled = false` to accept
the risk:

```csharp
builder.Services.AddOpenApiFeatureFlags(options => options.CanaryEnabled = false);
```

---

## Too much is hidden

The library hides a whole operation as soon as **any** flag governing it is disabled, because
multiple attributes are ANDed. That is intentional: an operation that needs two unreleased features
is itself unreleased.

So check for a controller-level attribute you forgot about:

```csharp
[OpenApiFeatureFlag("ExperimentalApi")]   // gates every action below it
public sealed class ExperimentalController : ControllerBase
```

If you want an operation documented when *any* flag is on, the current release does not support it —
give the operation a single canonical flag and express the OR in your flag store.

If only **part of a description** vanished, that is a `<gate>` fragment and not an attribute. The
library logs a warning when a gate is unusable — `GateWithoutFlag` (104) when it has no `flag`
attribute, `GateUnbalanced` (105) when it was never closed — so search the log for those before
looking anywhere else.

---

## A property is hidden but the document still mentions it

Two leftovers are possible and both are handled, so if you see this, it is worth reporting:

- **`required`** — the library removes a name from `required` when the property it referred to is
  gone, including the literal `null` some consumer schema filters add when they do
  `Required.Add(properties.FirstOrDefault(...).Key)`.
- **Prose** — XML doc cross-references, hand-written tag descriptions and intra-document anchors
  (`#v1-x-y-post`) that pointed at a removed operation are *not* rewritten. The library removes
  structure; it cannot rewrite a sentence. If a sibling property's `<summary>` mentions the hidden
  property by name, that text stays unless you gate it too:

  ```csharp
  /// The order total.
  /// <gate flag="LoyaltyProgram">See <c>loyaltyPoints</c> for the points balance.</gate>
  ```

  Gating a sentence with the same flag that gates the property keeps the two together. Anchors
  (`#v1-x-y-post`) are still yours to maintain — see [`descriptions.md`](descriptions.md).

---

## An unreferenced schema survived a removal

The reachability sweep removes component schemas that nothing references any more. It decides
reachability from the serialised document, and only runs in `Remove` mode when something was actually
gated. In `Annotate` and `Include` mode nothing is removed, so nothing is swept.

---

## The document changed even though I applied no attributes

It should not, and there is a regression test asserting it. Check that:

- you actually have zero `[OpenApiFeatureFlag]` attributes in the document's controllers and models,
- the mode is `Remove` (in `Include` the document is untouched by definition),

and if both hold, please open an issue with the two documents.
