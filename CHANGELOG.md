# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-10-01

### Added

- **`OpenApiFeatureFlags.AspNetCore`** — an adapter for the built-in `Microsoft.AspNetCore.OpenApi`
  document engine, as three transformers. Gated operations, parameters and schema properties behave
  as they do under Swashbuckle, `Annotate` and `Include` mean the same thing, and `<gate>` works in
  descriptions identically. It is **`net10.0` only**, and that is a documented cost rather than an
  oversight: the engine's 8.x and 9.x lines sit on the `Microsoft.OpenApi` 1.x document model, which is
  a different set of types, so covering them would mean a second implementation and only the 10.x one
  ships. See [`docs/aspnetcore.md`](docs/aspnetcore.md).
- The `<gate>` parser now lives in the core rather than in the Swashbuckle adapter, so both adapters
  share one implementation and cannot drift apart on quoting, nesting or unbalanced tags. This is an
  internal type: the public surface is unchanged.
- `ParameterKey`, which identifies an operation parameter by its action as well as its name, and
  `DocumentVisibilityPlan.IsParameterHidden` to query it.

### Changed

- **Breaking:** `UseFeatureManagement()` is now `AddFeatureManagementFlagSource()`, and `UseOpenFeature()`
  is now `AddOpenFeatureFlagSource()`. Both extend `IServiceCollection`, where `Add*` is the convention
  and `Use*` means middleware on an `IApplicationBuilder` — so the old names read as the wrong kind of
  call at the exact call site the documentation recommends.
- The once-per-document summary line reports `{n} kept` where it reported `{n} published`.
  "Published" was carrying two meanings at once — an element kept in the document, and the document
  being served — which made `0 published` readable as "nothing was published".
  `DocumentVisibilityPlan.ToLogSummary()` changed with it.

### Fixed

- A parameter no longer shares a key with a same-named parameter on another action of the same
  controller. `DocumentVisibilityPlan` aggregates decisions by key, so the two merged into one: the plan
  under-counted, and `IsMemberHidden`/`FlagsFor` answered for the wrong element. The generated document
  was always correct, because parameters are removed per operation from their own `ParameterInfo`, so
  this affected the plan, its counts and its queries rather than the document.

## [0.1.1] - 2026-09-30

### Fixed

- The readme claimed there had been no release and that `dotnet add package` would not find these
  packages. That was true when it was written and false from the moment `0.1.0` was published. The
  claim is replaced by a version badge that reads the live release from nuget.org, so it cannot rot the
  same way, and Installation now gives the real `dotnet add package` commands.

### Notes

- Readme and documentation only: no code, no assembly and no dependency differences from `0.1.0`. A
  new version is required because a package readme is read from inside the package and cannot be
  edited on nuget.org once published.

## [0.1.0] - 2026-09-30

First public release. All five packages ship together at this version.

### Added

- `OpenApiFeatureFlags.Abstractions` — dependency-free contracts: `[OpenApiFeatureFlag]`,
  `DocumentMode`, `IFeatureFlagSource`, `OperationKey`, `MemberKey`, `DocumentVisibilityDecision` and
  `DocumentVisibilityPlan`.
- `OpenApiFeatureFlags` — the engine-agnostic planner. AND semantics across attributes, fail-closed
  resolution, per-request memoisation through `HttpContext.Items`, one structured summary line per
  document, and the canary that refuses to publish a document whose every flag read failed.
- `OpenApiFeatureFlags.Swashbuckle` — operation, schema and document filters. Removes gated
  operations, parameters and schema properties; drops path items left empty, prunes properties left
  orphaned from `required`, sweeps unreferenced component schemas and prunes orphan tags. Emits
  `x-feature-flag` in `Annotate` mode.
- Description gating: `<gate flag="Name">…</gate>` inside an XML doc comment hides or keeps a
  fragment of `summary`, `description`, `remarks` or any other text that reaches the document.
  Nested gates AND; a gate with no `flag` attribute fails closed; an unclosed gate hides the rest of
  the string. The wrapper is stripped in every mode, including `Include`.
- `OpenApiFeatureFlags.FeatureManagement` — `FeatureManagerFlagSource` over `IFeatureManager`, plus the
  one-call `AddOpenApiFeatureFlagsWithFeatureManagement()` wiring.
- `OpenApiFeatureFlags.OpenFeature` — `OpenFeatureFlagSource` over the CNCF `IFeatureClient`, plus the
  one-call `AddOpenApiFeatureFlagsWithOpenFeature()` wiring. Passes `false` as OpenFeature's default
  value, which is what makes it fail closed for free.
- A runnable sample that generates its document at build time.

### Changed

- Log messages no longer carry internal decision numbers, and the once-per-document summary line says
  `flags involved` rather than `flags evaluated`. In `Annotate` mode nothing is ever evaluated — the flag
  source is deliberately never consulted — so the old wording claimed a provider call that had not
  happened, which is exactly the thing someone reads that line to find out.
  `DocumentVisibilityPlan.ToLogSummary()` changed to match.
- The offline fallback scope — the one used when there is no `HttpContext`, such as `swagger tofile` —
  is now per-thread. It was a single instance shared by the singleton planner, so two concurrent
  document generations on that path could share memoised flag values and each other's decisions, which
  fails quietly by producing a wrong document rather than loudly by crashing.
- Gate nesting is capped at 32 levels. Deeper nesting hides the innermost content and logs
  `GateTooDeep` (106) instead of recursing until the stack overflows, which cannot be caught and takes
  the process down.
- Symbol packages (`.snupkg`) are published alongside the `.nupkg` files. They were being built and
  then discarded, so stepping into this library from nuget.org reported the source as unavailable.

### Security

- Added [`SECURITY.md`](SECURITY.md) and [`docs/security.md`](docs/security.md). The latter states
  plainly what document gating does not do: it is not an access control, it cannot unpublish a document
  that has already been served, and `Annotate` mode discloses your flag names to anyone who can read
  the document.
- Added a CodeQL workflow, gave CI least-privilege `permissions`, and pinned every workflow action to a
  commit SHA.

### Notes

- Multi-targets `net8.0`, `net9.0` and `net10.0`.
- With no attributes applied, the produced document is byte-identical to one generated without the
  library. This is asserted by a regression test.
- Test projects run on xunit.v3 and Microsoft.Testing.Platform, which the .NET 10 SDK selects through
  `global.json`.

[0.2.0]: https://github.com/dogukandemir/OpenApiFeatureFlags/releases/tag/v0.2.0
[0.1.1]: https://github.com/dogukandemir/OpenApiFeatureFlags/releases/tag/v0.1.1
[0.1.0]: https://github.com/dogukandemir/OpenApiFeatureFlags/releases/tag/v0.1.0
