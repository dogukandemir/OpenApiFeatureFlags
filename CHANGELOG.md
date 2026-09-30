# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `OpenApiFeatureFlags.Abstractions` — dependency-free contracts: `[OpenApiFeatureFlag]`,
  `DocumentMode`, `IFeatureFlagSource`, `OperationKey`, `MemberKey`, `DocumentVisibilityDecision` and
  `DocumentVisibilityPlan`.
- `OpenApiFeatureFlags` — the engine-agnostic planner. AND semantics across attributes, fail-closed
  resolution, per-request memoisation through `HttpContext.Items`, one structured summary line per
  document, and the D6 canary that refuses to publish a document whose every flag read failed.
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

### Notes

- Multi-targets `net8.0`, `net9.0` and `net10.0`.
- With no attributes applied, the produced document is byte-identical to one generated without the
  library. This is asserted by a regression test.
- Test projects run on xunit.v3 and Microsoft.Testing.Platform, which the .NET 10 SDK selects through
  `global.json`.

[Unreleased]: https://github.com/dogukandemir/OpenApiFeatureFlags/commits/main
