# What does this change?

<!-- One or two sentences. Link the issue if there is one: "Closes #12". -->

## Why

<!-- The problem this solves. If it is a behaviour change, say what the old behaviour was. -->

## How to check it

<!--
The reviewer needs to be able to verify this, not just read it. Give the commands, and say what the
output should look like if the change is correct.
-->

```shell
dotnet build --configuration Release
dotnet test
```

## Checklist

- [ ] `dotnet build --configuration Release` is clean — it treats warnings as errors.
- [ ] `dotnet test` passes on all three target frameworks.
- [ ] Public API changes are recorded in `PublicAPI.Unshipped.txt` (the build tells you the exact
      lines; `./eng/update-public-api.ps1 -ProjectDir <project>` applies them).
- [ ] Commits are signed off (`git commit -s`), per [`CONTRIBUTING.md`](../CONTRIBUTING.md).
- [ ] If this contradicts a settled decision in [`docs/design.md`](../docs/design.md), the change argues for
      changing that decision rather than working around it.

## Behaviour, if it changed

<!--
Delete this section when nothing user-visible changed.

Two invariants are easy to break by accident, and a reviewer will look for them:
  * Fail closed — an element whose flags cannot be evaluated is hidden, never published.
  * The library changes what a document says, never what an endpoint does.
-->

- Does it still fail closed when a flag cannot be read?
- Does the document remain byte-identical when no attributes and no `<gate>` fragments are present?

## Anything unresolved

<!-- Known gaps, follow-up work, or something you deliberately did not do. "Nothing" is fine. -->
