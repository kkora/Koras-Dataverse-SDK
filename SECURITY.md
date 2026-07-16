# Security Policy

## Supported versions

Security fixes are provided for the latest released minor version, and — after 1.0 — for the
previous major version as described in [docs/release/versioning.md](docs/release/versioning.md).

## Reporting a vulnerability

**Do not open a public issue for security problems.**

Report privately via GitHub's *Security → Report a vulnerability* (private vulnerability
reporting) on this repository, or email **security@korastech.com**. Include reproduction steps,
affected package/version, and impact assessment if possible.

We aim to acknowledge reports within **3 business days** and to provide a remediation plan or
fix within **90 days**. We will credit reporters in release notes unless anonymity is requested.

## Scope notes

- The SDK never stores credentials; secrets flow through `Azure.Identity` credentials or a
  caller-supplied token provider. Reports about secret handling in *sample code* are welcome too.
- Query builders are designed to be injection-safe; any bypass of value encoding in
  `ODataFilterBuilder`, `ODataQuery`, or the FetchXML builder is a security bug — please report it.
- See [docs/security/threat-model.md](docs/security/threat-model.md) for the full threat model.
