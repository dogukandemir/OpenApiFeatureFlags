# Gating part of a description

XML doc comments become `description` fields in the published document. Sometimes only *part* of that
prose is gated — a sentence about a flag, a code sample for an unreleased parameter — and you cannot
attach an attribute to half a sentence.

Use `<gate>` for that:

```csharp
public sealed class Order
{
    public string? Id { get; set; }

    /// <summary>
    /// The order total.
    /// <gate flag="LoyaltyProgram">Points are shown in <c>loyaltyPoints</c>.</gate>
    /// </summary>
    public decimal Total { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}
```

With `LoyaltyProgram` enabled:

> The order total. Points are shown in `loyaltyPoints`.

With it disabled:

> The order total.

`<gate>` works anywhere a string reaches the document: operation `summary` and `description`,
`remarks`, parameter descriptions, request and response descriptions, schema and property
descriptions, the document-level description, and tag descriptions. Nested gates **AND**, exactly
like stacked attributes.

## The rules

| Situation | Result |
|---|---|
| Flag enabled | The inner text is kept, with the wrapper removed. |
| Flag disabled | The inner text is removed. |
| No `flag` attribute | The content is hidden, and `GateWithoutFlag` (104) is logged. Fail closed. |
| Closing tag missing | Everything from the gate to the end of the string is hidden, and `GateUnbalanced` (105) is logged. |
| Flag cannot be read | Hidden. The same fail-closed rule as attributes. |

Gates are resolved in **every** mode, including `Include`. The wrapper is markup the library
invented, so it must never reach a consumer of your document; only the *content* is gated. That means
a gate behaves as follows:

| Mode | Gate with the flag enabled | Gate with the flag disabled |
|---|---|---|
| `Remove` | Inner text kept. | Inner text removed. |
| `Annotate` | Inner text kept. | Inner text kept — `Annotate` never deletes prose. |
| `Include` | Inner text kept. | Inner text kept. |

`Annotate` keeps the text because that mode's contract is that the document stays complete and gains
`x-feature-flag` metadata. A sentence has nowhere to carry that metadata, so the only honest options
are to keep it or delete it, and deleting it would break the mode.

## Writing it without compiler noise

`<gate>` is not a real XML doc element, but the compiler does not care: unknown elements with valid
XML syntax are passed through untouched and no warning is emitted. You only get `CS1570` if the
element is *malformed*, so always close it:

```csharp
/// <gate flag="LoyaltyProgram">…</gate>    // fine
/// <gate flag="LoyaltyProgram">…           // CS1570: the comment is no longer well-formed XML
```

Self-closing is also valid — `<gate flag="LoyaltyProgram" />` hides nothing, which makes it useful
only as a scratch marker.

You must have XML comments switched on for any of this to matter:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

and the consumer must include them:

```csharp
options.IncludeXmlComments(typeof(Order).Assembly);
```

## What it cannot do

**It cannot tell that a sentence refers to a member that was just removed.** If you gate a property
and separately write prose naming it, the property disappears and the prose does not:

```csharp
/// <summary>
/// The order total. See <c>loyaltyPoints</c> for the points balance.   // ← not gated
/// </summary>
public decimal Total { get; set; }

[OpenApiFeatureFlag("LoyaltyProgram")]
public int LoyaltyPoints { get; set; }
```

With the flag off you publish "See `loyaltyPoints` for the points balance" and no `loyaltyPoints`.
Detecting that would mean parsing English. The fix is the one you would write anyway — gate the
prose with the same flag, so the two travel together:

```csharp
/// The order total.
/// <gate flag="LoyaltyProgram">See <c>loyaltyPoints</c> for the points balance.</gate>
```

There is a test pinning the first behaviour (`ADanglingReferenceSurvivesWhenTheAuthorDidNotGateTheProse`)
so the limit cannot change silently, and a companion test showing the second one works.

## Logging

Gate decisions use the same logger as the rest of the library:

| Event | Id | Meaning |
|---|---|---|
| `DescriptionFragmentHidden` | 103 | A gated fragment was removed, and which flag did it. |
| `GateWithoutFlag` | 104 | A `<gate>` had no usable `flag` attribute; the content was hidden. |
| `GateUnbalanced` | 105 | A `<gate>` was never closed; the remainder was hidden. |

Events 104 and 105 are warnings — they always mean the author made a mistake, so you will see them in
your logs rather than wondering why prose vanished.
