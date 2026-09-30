# Contributing

Thanks for taking the time. This project is small and the rules are short.

## Ground rules

By contributing you agree that:

- **Inbound = outbound.** Your contribution is licensed under the MIT licence in [`LICENSE`](LICENSE).
  There is no CLA and there is no NOTICE file to maintain.
- **You sign off your commits** (Developer Certificate of Origin). Add a `Signed-off-by` line with
  `git commit -s`, certifying that you wrote the change or otherwise have the right to submit it
  under this licence.

## Before you open a pull request

```shell
dotnet build --configuration Release
dotnet test
```

Both must be clean. The build treats warnings as errors, and the test projects run on
[Microsoft.Testing.Platform](https://aka.ms/dotnet-test-mtp-error) — that is why `global.json`
selects the runner. In MTP mode, options that `dotnet test` does not recognise are forwarded to the
test application, so avoid `--nologo` and friends; use `-p:TargetFramework=net8.0` to run one
framework.

## The two rules that will get a pull request sent back

**1. The public API is a permanent contract.** Every public type and member is listed in
`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` next to its project, and the
`Microsoft.CodeAnalysis.PublicApiAnalyzers` package fails the build when the two drift apart. When
you add public surface, the build tells you the exact line to add; when you remove it, the build
tells you the line to delete.

**2. `OpenApiFeatureFlags.Abstractions` has no dependencies.** Not a package reference, not a
project reference. Contract assemblies must be able to carry the attribute without being dragged
onto Swashbuckle or a feature-flag library. A test asserts the built assembly references nothing but
the framework.

## Design decisions

[`BACKLOG.md`](BACKLOG.md) is the source of truth. It records what was decided (D1–D17), what was
*measured* rather than assumed, and what is still open (Q1–Q6). If your change contradicts a locked
decision, the pull request needs to argue for changing that decision rather than quietly working
around it.

Two invariants that are easy to break by accident:

- **Never cache flag state in a filter field.** Document engines construct filters once for the
  lifetime of the application, so an instance field holds the first request's snapshot forever.
  Per-request state belongs in `HttpContext.Items`; the core does this for you.
- **Fail closed.** A flag that cannot be evaluated hides the element. If you find yourself catching
  an exception and returning `true`, that is a bug.

## Tests

Bug fixes and behaviours come with a test. Two tests are load-bearing and should not be weakened:

- `WithNoGatedElementsTheDocumentIsByteIdentical` — with nothing gated, the library must be
  invisible.
- `GatedDocumentMatchesTheGoldenSnapshot` — the snapshot lives in
  `tests/OpenApiFeatureFlags.Swashbuckle.Tests/Golden/`. If a change legitimately alters the
  document, update the file in the same commit so the diff is reviewable.

## Reporting a problem

Include the mode, the attribute placement, the log line from the `OpenApiFeatureFlags` category, and
— for "something is still visible" — the relevant part of the generated document. The
troubleshooting guide in [`docs/troubleshooting.md`](docs/troubleshooting.md) lists the questions
worth answering up front.
