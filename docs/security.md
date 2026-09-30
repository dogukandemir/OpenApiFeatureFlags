# Security considerations

This page is about what the library does to the security posture of the application that uses it. For
reporting a vulnerability *in* the library, see [`SECURITY.md`](../SECURITY.md).

## The one rule

**This library changes what a document says. It never changes what an endpoint does.**

Hiding an operation from the document leaves its route reachable and its authorization unchanged.
Hiding a property leaves it serialised in responses. That is deliberate, and no configuration changes
it: a documentation decision must not be able to silently become a security control.

So: **do not use document gating to protect an endpoint.** If an endpoint must not be callable, do not
call it — gate it at runtime with your own feature-flag check, and add authentication and
authorization on top. Treat what this library does as making unreleased surface *less discoverable*,
which is a marketing and support problem, not a security boundary.

## What gating does not do

### It does not unpublish anything that was already published

Once a document has been served — to a customer, a partner, a CDN, a search engine, or a copy someone
saved — gating an element later does not recall it. Google does not un-index a page because the
document changed, and a customer who read the endpoint list last week still has it.

If your document is public, treat everything that has ever appeared in it as public permanently, and
gate *before* the first release rather than after.

### It does not defeat caching

Swashbuckle generates the document per request and does not cache it, so a flag flip is reflected on
the next request. Anything you put in front of it is a different story:

- An HTTP cache that stores `swagger.json` will keep serving the old document until it expires. If you
  gate something, the cached copy keeps advertising it.
- A CDN or reverse proxy behaves the same way, and can serve one tenant's document to another if the
  response is cached without varying on whatever identifies the tenant.

If you serve the document to different audiences, set `Cache-Control` deliberately — typically
`no-store` for authenticated audiences, or a short `max-age` if you accept staleness.

## What gating can reveal

### `Annotate` mode publishes your flag names

`Annotate` keeps every element and adds `x-feature-flag` metadata so a portal can filter client-side.
That means the document contains:

- the name of every flag involved in the document, in a document-level `x-feature-flag` array, and
- the flags governing each gated **operation**, as `x-feature-flag` on that operation.

A gated **schema property** is not individually marked in this mode, so a reader learns the flag name
from the root array but cannot see which property it gates. That limits what a client-side filter can
do; it does not limit the disclosure.

Anyone who can read the document can read your flag names and see which endpoints they are attached
to. If your flag names encode unreleased product plans (`NewCheckout`, `EnterpriseSsoTier`), that is a
roadmap disclosure to whoever can reach the document.

Use `Annotate` only where the audience is already allowed to know that much — an internal portal, or
an authenticated document. Use `Remove` when the audience is not.

`Include` mode publishes the entire document and evaluates nothing; it is the "off switch" and has the
same disclosure properties as not using the library at all.

### Logging carries flag names too

The library logs the flag names it evaluated at `Debug`, and one summary line per document that names
the flags involved:

```
OpenApiFeatureFlags finished the document in mode Remove: 3 gated element(s) hidden, 0 published, flags involved Loyalty, NewCheckout.
```

Flag names are configuration, not secrets, but they travel wherever your logs travel. If logs are
shipped to a third party, that is the same disclosure as `Annotate` mode. Raise the log level for the
`OpenApiFeatureFlags` category if that matters to you — the summary line is the useful one during
incident response, so consider keeping it and dropping `Debug`.

## Availability trade-offs

These are real, and worth deciding about rather than discovering.

### The canary can take your document endpoint down

Fail-closed behaviour (an unreadable flag hides the element) is only safe while your flag provider
answers at all. If a document contains gated elements and **not one** flag read succeeded, the library
throws `FeatureFlagSourceUnavailableException` instead of publishing a document that is silently
missing released endpoints.

That is a deliberate trade: a broken document endpoint is loud and gets fixed, whereas a silently
truncated document is quiet and gets shipped. If you would rather serve a possibly-truncated document
than a 500, set `CanaryEnabled = false` — and accept that a provider outage then silently hides
released surface.

### A slow flag provider slows down document generation

Flags are evaluated while the document is being built, which for `swagger.json` is a request. A
provider that blocks on network I/O adds that latency to the request, and a per-flag round trip adds
it once per flag.

Memoisation limits this: each flag is read at most once per request. The default adapters additionally
lean on the caching the underlying SDK already does. If your provider is genuinely slow, that is
open question Q4 in [`BACKLOG.md`](../BACKLOG.md) — an async resolution path — rather than something to
solve with a longer timeout.

### Blocking on asynchronous providers

`IFeatureFlagSource` is synchronous, and the document engine's pipeline is synchronous, so the
`FeatureManagement` and `OpenFeature` adapters block on `Task` results. Under ASP.NET Core there is no
`SynchronizationContext`, so this does not deadlock. If you host document generation somewhere that
*does* have one — a desktop UI thread, or legacy ASP.NET — blocking there can deadlock, and that is a
reason to implement `IFeatureFlagSource` yourself against a synchronous store rather than to use those
adapters.

## What the library does not touch

Useful when you are filling in a security questionnaire:

- **It reads no configuration and holds no credentials.** It never sees a connection string. Azure App
  Configuration works through *your* `Microsoft.FeatureManagement` registration, reached through
  `IFeatureManager`; there is no Azure-specific code in these packages.
- **It performs no network or file I/O of its own.** The only outbound call is the one your
  `IFeatureFlagSource` makes, which is your code.
- **It does not touch request or response bodies**, does not read headers, does not authenticate
  anything, and does not persist anything. Per-request state lives in `HttpContext.Items` and dies
  with the request.
- **It does not log request or response data.** The `<gate>` warnings log what you wrote: the
  offending tag, and — for an unbalanced gate — the text from that tag to the end of the description.
  That is markup from your own XML doc comments, not end-user data.
- **It does not add middleware**, so it changes no request pipeline behaviour.

## Recommended posture

| Situation | What to do |
|---|---|
| Public, unauthenticated document | `Remove` mode. Never `Annotate` — it discloses flag names. |
| Authenticated document, internal audience | `Annotate` is reasonable. |
| Endpoint must not be callable | Do not rely on this library. Gate at runtime and authorize properly. |
| Document was public once | Assume the content is public forever. |
| Flag provider can be down | Decide deliberately between the canary (loud 500) and `CanaryEnabled = false` (silent truncation). |
