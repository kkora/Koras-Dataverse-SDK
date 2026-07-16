# ADR-0005: Plain CLR value model

## Status

Accepted — 2026-07-16

## Context

The classic `Microsoft.Xrm.Sdk` model wraps Dataverse attribute values in SDK-specific types:
`OptionSetValue` for choices, `Money` for currency, `EntityReference` for lookups,
`OptionSetValueCollection` for multi-select. Working with them requires constant wrapping and
unwrapping (`((Money)e["revenue"]).Value`), obscures ordinary .NET code, and infects domain
models with SDK types.

The Web API's JSON representation is already plain: choices are integers, money is a decimal,
dates are ISO strings, lookups are `_field_value` GUIDs written via `field@odata.bind`
navigation properties. The SDK's differentiator list explicitly includes "plain CLR value
model (no `OptionSetValue`/`Money` wrappers)" (master plan §1), and KDV-002 requires
`@odata.bind` to be handled automatically.

Lookups are the one case where a bare CLR primitive is insufficient: a reference needs both a
table logical name and an id to be addressable.

## Decision

Attribute values in `Entity` (and in typed POCO mapping) will be **plain CLR types**:

| Dataverse column type | CLR type |
|---|---|
| Text/memo | `string` |
| Whole number | `int` |
| BigInt | `long` |
| Decimal | `decimal` |
| Float | `double` |
| Currency | `decimal` |
| Choice (option set) | `int` (nullable when absent) |
| Multi-select choice | collection of `int` |
| Yes/No | `bool` |
| Date/time | `DateTime` / `DateTimeOffset` per column behavior |
| Unique identifier | `Guid` |
| Lookup | `EntityReference` (logical name + id) |

`EntityReference` (in `Koras.Dataverse.Abstractions`) is the **only** wrapper type, because a
lookup is inherently a pair. On write, the client translates an `EntityReference` value into
the correct `field@odata.bind` navigation property (including collection-valued and
polymorphic lookups); on read, `_field_value` annotations materialize back into
`EntityReference` with the logical name taken from the accompanying lookup annotations.
Consumers never write `@odata.bind` strings by hand.

Formatted values and other OData annotations are surfaced through dedicated accessors on the
result model rather than polluting the attribute value space (exact accessor shape subject to
implementation review).

## Consequences

- Consumer code reads like ordinary .NET: `e["revenue"] = 25_000m;`,
  `e["statecode"]` is an `int` — no unwrap ceremony, easy mocking, natural pattern matching.
- POCO mapping (KDV-002) maps straight to natural property types (`decimal Revenue`,
  `int StateCode`, `EntityReference PrimaryContact`).
- We give up wrapper-type niceties: no formatted label bundled inside a choice value, no
  currency symbol on money. Labels come from formatted-value accessors or the metadata client
  (KDV-006) instead.
- The write path must reliably distinguish "regular attribute" from "lookup" — driven by the
  value being an `EntityReference` — and must get `@odata.bind` right for all lookup shapes;
  this carries dedicated unit tests (master plan §6, entity conversion).
- Choice values as raw `int` means no compile-time choice safety in the late-bound model;
  that gap is closed later by source-generated early-bound models (KDV-016), not by wrappers.
- Migration from `Microsoft.Xrm.Sdk` code requires value-model translation; the comparison
  guide (adoption strategy, master plan §1) documents the mapping table above.

## Alternatives considered

- **Adopt `Microsoft.Xrm.Sdk` wrapper types.** Rejected: requires referencing or cloning the
  legacy SDK's type closure, drags the exact ergonomics this SDK exists to escape, and leaks
  third-party-style types through the whole API.
- **Own lightweight wrappers (`Choice`, `MoneyValue`).** Rejected: recreates the unwrap tax
  with new names; no information gain over `int`/`decimal` once formatted values are
  accessible separately.
- **Fully dynamic/JSON-facing model (`JsonElement` values).** Rejected: pushes wire-format
  concerns onto consumers, defeats the point of an SDK, and couples the public surface to
  `System.Text.Json`.
- **Strings for everything (raw OData literals).** Rejected: type-unsafe, culture-sensitive
  formatting bugs, unusable for querying and batching ergonomics.
