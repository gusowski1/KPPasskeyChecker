# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

`release.ps1` uses the section for the version being released as the GitHub release notes,
so keep each `## [x.y.z]` heading and its body accurate before running a release.

## [Unreleased]

## [0.5.0] - 2026-07-21

### Added
- "[No Data]" in the Passkey Support column when the directory was consulted and the entry's
  domain is simply not listed, so "checked, nothing found" can be told apart from "not loaded yet"

### Changed
- stored-passkey status is now always shown in bracket form ("[Active]"), consistent with
  "[Inactive]" and with the directory values next to it

### Fixed
- errors during a background directory refresh are surfaced instead of being silently lost

### Security
- a malformed or hostile signature file could make signature verification run forever at full CPU;
  packet lengths are now validated and verification always fails closed

## [0.4.0] - 2026-06-27

### Added
- update README: document [Active]/[Inactive] column status and stored-passkey detection
- rename Shared namespace to KeeRadar.Shared.*, add [Active]/[Inactive] passkey status prefix
- Shared sync workflow (sync-shared.ps1 + agent rules)

## [0.3.0] - 2026-06-26

### Added
- Plugin icon (16Ã—16, KeePass key with navy badge) shown in the Tools menu, entry
  context menu, and detail dialog title bar.
- Self-check harness (`tools/SelfCheck`) for offline regression testing of core logic
  (parsing, scope mapping, domain matching, PGP verification path) without a running
  KeePass process.
- Self-check runs automatically at the start of the release Prepare stage; the release
  aborts immediately if any check fails.

### Fixed
- GDI handle leak: the native HICON created by `Bitmap.GetHicon()` is now released via
  `DestroyIcon` in `Terminate` (`Icon.Dispose` does not free it).
- PGP packet parser: added bounds checks before multi-byte reads in the signature and
  unhashed-subpacket paths to prevent out-of-bounds reads on malformed input.

## [0.2.0] - 2026-06-24
### Added
- Per-entry detail window: double-click the **Passkey Support** column cell to open a
  KeePass-native dialog showing the entry's passkey support (passwordless / 2nd factor),
  documentation and recovery links, notes and regions, with the required attribution.

### Changed
- The manual **Refresh now** action now reports failures clearly, distinguishing the
  cause (network / data format / signature verification) while continuing to show the
  last cached data.

### Fixed
- The Public Suffix List cache no longer fails to update after its 7-day TTL (atomic
  replace instead of an unconditional `File.Move`).

### Security
- JSON size cap aligned to the 16 MB transport cap; parse failures are now surfaced
  instead of being silently dropped.

## [0.1.0] - 2026-06-23
### Added
- Initial public release: a **Passkey Support** entry-list column backed by the Passkeys
  Directory (2factorauth), with local caching, PGP signature verification of the data,
  and KeePass update-check wiring.