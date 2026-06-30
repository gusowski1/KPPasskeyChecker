# KeeRadar Shared Infrastructure

This directory contains plugin-agnostic infrastructure shared across all KeeRadar plugins
(KPPasskeyChecker, KP2FAChecker, and any future plugins in the KeeRadar family).

**Canonical source:** `KPPasskeyChecker/src/Shared`. Edit only there — never in a
consumer repo's synced copy of this directory. Changes are propagated to consumer
repos by running that repo's `sync-shared.ps1` (e.g. `KP2FAChecker/sync-shared.ps1`),
which mirrors this directory verbatim.

## Namespace

All types live under `KeeRadar.Shared.*` — plugin-neutral by design so that consumers
such as KP2FAChecker do not carry KPPasskeyChecker identifiers in their stack traces.

## Contents

| Subfolder | Purpose |
|-----------|---------|
| `Caching/` | `ILocalCache`, `CacheEntry`, `FileSystemJsonCache` — atomic file-based JSON cache |
| `Http/` | `ConditionalHttpFetcher` (ETag/If-None-Match), `FetchResult`, `FetchOutcome`, `UserAgent` |
| `DomainMatching/` | `DomainCandidateGenerator` — walks host → eTLD+1 via Public Suffix List |
| `KeePassUi/` | `PluginSettingsStoreBase`, `EntryDetailForm`, `EntryDetailRow` |
| `Pgp/` | Generic inline OpenPGP signature verifier (RSA-4096/SHA-512, BCL-only, no BouncyCastle) |

## Constraints

- **C# 5 compatible** — the `.plgx` is recompiled on the user's machine by KeePass using C# 5.
  No modern C# syntax, no NRT, no records (except via the `IsExternalInit` polyfill).
- **No third-party dependencies** — only the .NET BCL and `KeePass.exe` as a reference.
- **No data-source-specific logic** — passkey vs. 2FA specifics belong in the plugin project,
  not here.
