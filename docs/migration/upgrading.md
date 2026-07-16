# Upgrading

## Current state: nothing to upgrade yet

The SDK is at its first preview (`0.1.0-preview`). There are no earlier published versions,
so there is no upgrade path to document — this page exists so the process is established
before it is needed.

## How upgrades will work

When new versions ship, this page will carry a section per release, newest first, each with:

- the version pair (`from` → `to`) and whether the step contains breaking changes,
- the exact package-reference bump,
- a mechanical, step-ordered migration list (compile-breaking changes first, then behavioral
  changes to review),
- links to the corresponding [breaking changes](breaking-changes.md) entries and the
  changelog.

Expectations by version step (from the [versioning policy](versioning-policy.md)):

| Step | What to expect |
|---|---|
| Patch (`x.y.1` → `x.y.2`) | Drop-in. Bug/security fixes only; no action beyond updating the reference |
| Minor, 1.0+ (`1.1` → `1.2`) | Drop-in. Additive API only; new `[Obsolete]` warnings may appear — heed them, they announce the next major's removals |
| Minor, 0.x (`0.1` → `0.2`) | **May break.** Read the changelog's explicitly marked breaking entries before updating |
| Major (`1.x` → `2.0`) | Breaking. Follow this page's step list for that release |

Because all `Koras.Dataverse*` packages version together, always bump every referenced package
to the same version in one commit:

```bash
dotnet add package Koras.Dataverse --version <new>
dotnet add package Koras.Dataverse.Abstractions --version <new>   # if referenced directly
dotnet add package Koras.Dataverse.FetchXml --version <new>       # if referenced directly
dotnet add package Koras.Dataverse.OpenTelemetry --version <new>  # if referenced
```

## During the preview

Between `-preview.N` builds the API may change without a deprecation window. Preview adopters
should read the changelog for every preview bump and treat this page as authoritative once
`0.1.0` stabilizes.
