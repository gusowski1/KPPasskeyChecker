# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

`release.ps1` uses the section for the version being released as the GitHub release notes,
so keep each `## [x.y.z]` heading and its body accurate before running a release.

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
