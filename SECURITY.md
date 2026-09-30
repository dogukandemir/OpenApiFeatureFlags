# Security policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.** A public issue tells everyone about the
problem before there is a fix, including the people who would use it against your application.

Use GitHub's private vulnerability reporting instead:

**[Report a vulnerability](https://github.com/dogukandemir/OpenApiFeatureFlags/security/advisories/new)**

That keeps the report private to you and the maintainer until an advisory is published. If you cannot
use it for some reason, open an issue that says only "I have a security report and need a private
channel" — with no details — and a private channel will be arranged.

A useful report includes what you did, what happened, what you expected, the affected version, and
the smallest reproducer you can manage. A proof of concept is welcome; a working exploit against
someone else's system is not.

## What to expect

This is a small, single-maintainer project, so these are best-effort targets rather than a service
level agreement:

| Stage | Target |
|---|---|
| Acknowledgement that the report was received | within 3 working days |
| An initial assessment, including whether it is in scope | within 10 working days |
| A fix, or a decision not to fix with the reasoning | depends on severity and complexity |

You will be credited in the advisory unless you ask not to be. If a fix is needed, the advisory is
published at the same time as the release that carries it, so that users have something to upgrade
to before the details are public.

## Supported versions

The library is pre-1.0, and fixes are released as a new version rather than as patches to old ones.
Only the latest published version is supported.

| Version | Supported |
|---|---|
| Latest published | Yes |
| Anything older | No — upgrade |

## Scope

In scope — the published packages:

- `OpenApiFeatureFlags`
- `OpenApiFeatureFlags.Abstractions`
- `OpenApiFeatureFlags.Swashbuckle`
- `OpenApiFeatureFlags.FeatureManagement`
- `OpenApiFeatureFlags.OpenFeature`

Out of scope:

- `samples/OpenApiFeatureFlags.Sample`. It is a demonstration, is deliberately unauthenticated, and is
  not meant to be deployed anywhere.
- Vulnerabilities in dependencies. Report those upstream; Dependabot watches them here, and a report
  that a pinned dependency is affected is still welcome as an issue.
- Anything requiring an attacker to already control your build, your configuration, or a flag provider.

## The security model, in one place

The library changes what an API document says. It never changes what the API does. That framing
matters for judging a report, so the guarantees are:

- **Fail closed.** An element whose flags cannot be evaluated is hidden, never published. This
  includes a flag provider that throws, a flag that is not configured, and a `<gate>` fragment with a
  malformed or missing `flag` attribute.
- **Fail loudly instead of quietly deleting.** Failing closed is only safe while the flag provider
  answers at all. If a document contains gated elements and *not one* flag read succeeded, the
  library throws rather than publishing a document missing released endpoints. This is the canary
  (`CanaryEnabled`, on by default).
- **A document is never hidden behind runtime behaviour.** Hiding an endpoint from the document
  leaves it routable. This is deliberate, and it is the one rule the design refuses to bend: a
  documentation decision must not silently become a security control. **Do not use this library to
  protect an endpoint.** Use authentication and authorization for that, and treat document gating as
  what it is — keeping unreleased surface out of sight.

The one guarantee this library deliberately does **not** make is confidentiality of the document
itself. If a document is reachable without authentication, everything the library chose to keep in it
is public. See [`docs/security.md`](docs/security.md) for the consequences, including the fact that
`Annotate` mode publishes your feature flag names.
