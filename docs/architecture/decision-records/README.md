# Architecture Decision Records

This directory holds the Architecture Decision Records (ADRs) for the Koras Dataverse SDK.
Each ADR captures one significant, hard-to-reverse decision: the context that forced it, the
decision itself, its consequences, and the alternatives that were considered and rejected.
ADRs elaborate [`docs/planning/master-plan.md`](../../planning/master-plan.md); when a
conflict is found, the master plan wins until resolved by a superseding ADR.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| [ADR-0001](ADR-0001-web-api-first-transport.md) | Web API–first transport | Accepted | 2026-07-16 |
| [ADR-0002](ADR-0002-target-frameworks.md) | Target frameworks and per-TFM dependency pinning | Accepted | 2026-07-16 |
| [ADR-0003](ADR-0003-package-layout-and-di.md) | Package layout and DI registration placement | Accepted | 2026-07-16 |
| [ADR-0004](ADR-0004-authentication-tokencredential.md) | TokenCredential-based authentication behind IDataverseTokenProvider | Accepted | 2026-07-16 |
| [ADR-0005](ADR-0005-plain-clr-value-model.md) | Plain CLR value model | Accepted | 2026-07-16 |
| [ADR-0006](ADR-0006-exception-based-error-model.md) | Exception-based error model | Accepted | 2026-07-16 |
| [ADR-0007](ADR-0007-built-in-resilience.md) | Built-in resilience instead of a resilience dependency | Accepted | 2026-07-16 |
| [ADR-0008](ADR-0008-telemetry-activitysource.md) | Telemetry via ActivitySource/Meter with zero OTel dependency | Accepted | 2026-07-16 |
| [ADR-0009](ADR-0009-central-package-management.md) | Central Package Management | Accepted | 2026-07-16 |
| [ADR-0010](ADR-0010-public-api-tracking.md) | Public API tracking and package validation | Accepted | 2026-07-16 |

## Format

Every ADR uses the same sections, in order:

1. **Status** — `Proposed`, `Accepted`, `Superseded by ADR-XXXX`, or `Rejected`, plus the
   decision date.
2. **Context** — the forces at play: requirements, constraints, and the problem that made a
   decision necessary. Written so a newcomer can understand it without the discussion thread.
3. **Decision** — the decision, stated in full sentences in the active voice ("We will …").
4. **Consequences** — what becomes easier, what becomes harder, and what obligations the
   decision creates, including negative consequences we accept.
5. **Alternatives considered** — each realistic alternative with the reason it was rejected.

## Process

- **When to write one.** Any decision that is expensive to reverse, that constrains the public
  API or package boundaries, that adds/rejects a dependency, or that future maintainers would
  otherwise re-litigate. Routine implementation choices do not need an ADR.
- **How.** Copy the section skeleton from any existing ADR, take the next sequential number
  (`ADR-00NN-short-kebab-title.md`), open a PR. The ADR is discussed on the PR; merging with
  `Status: Accepted` is the acceptance act. Update the index table in this README in the same
  PR.
- **Changing a decision.** ADRs are immutable once accepted, except for status changes and
  typo fixes. To reverse or amend a decision, write a new ADR that supersedes the old one and
  mark the old ADR `Superseded by ADR-XXXX`. If the change affects the master plan, the same
  PR must update the master plan.
- **Numbering.** Sequential, never reused, even for rejected ADRs.
