# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**KPPasskeyChecker** is a KeePass 2.x plugin that checks whether the domain of a saved entry supports passkeys, using the official Passkeys Directory API (`passkeys-api.2fa.directory`). It is intentionally separate from any 2FA plugin. Shared infrastructure is compiled into the `.plgx` at build time — there is no shared binary dependency at runtime.

## Build

Canonical build:

```
.\build.ps1
```

The single source of truth lives under **`src\`** (`src\Shared` + `src\KPPasskeyChecker`). `build.ps1` produces **both** shipping artifacts into `build\`:

- **`KPPasskeyChecker.plgx`** — KeePass packages the sources (`KeePass.exe --plgx-create`) and compiles them on the user's machine at load time (C# 5). KeePass needs one flat folder containing a `.csproj` + all `.cs`, so `build.ps1` **generates** that flat folder in `%TEMP%` from `src\` on demand, packages it, then deletes it. There is **no committed copy** of the sources.
- **`KPPasskeyChecker.dll`** — the same sources compiled with the in-box `csc.exe` (C# 5, `/optimize+`) into a single self-contained assembly (`Shared` compiled in; no third-party deps). The build asserts its `ProductName` is `KeePass Plugin`, otherwise KeePass silently ignores the DLL.

Both come from the same `src\` sources, so they are functionally identical — ship both, install only one.

**Visual Studio / `dotnet build`** also build the plugin: `KPPasskeyChecker.sln` → `src\KPPasskeyChecker\KPPasskeyChecker.csproj` (SDK-style, **`LangVersion 5`**; `Shared` linked in as source → single self-contained DLL). A **Release** build runs a post-build step that also emits the `.plgx` (`build.ps1 -PlgxOnly`) and copies the DLL into `build\`; Debug builds skip that for fast iteration.

**Prerequisites:** `KeePass.exe` at `Libs\KeePass.exe` (not in the repo / not on NuGet; used for packaging and as a compile reference, never bundled).

**Minimum versions (empirically verified 2026-06-25 via compile-bisection in `probe\`):**

| Axis | Minimum | Determining factor |
|---|---|---|
| KeePass | **2.18** | `Plugin.UpdateUrl` introduced in 2.18 |
| .NET Framework | **4.6** | `HashAlgorithmName` / `RSASignaturePadding` introduced in .NET 4.6 (`Shared/Pgp/`) |

Both prereqs are embedded in the `.plgx` (`-plgx-prereq-kp:2.18 -plgx-prereq-net:4.6` in `build.ps1`).
Use **KeePass 2.18** as `Libs\KeePass.exe` to act as a compile-time tripwire: the build fails immediately
if new code accidentally references a newer KeePass API. Bump the reference *and* the prereqs in
`build.ps1` only when a newer API is intentionally required.

Target framework: `.NET 4.8` (`net48`). UI is WinForms (not WPF). **Keep all source C# 5-compatible** — the `.plgx` is recompiled as C# 5 on the user's machine, so modern C# / NRT syntax would break it.

## Release

Releases are scripted by **`release.ps1`** (repo root): a three-stage **branch + PR** flow.
**`CHANGELOG.md`** (repo root, [Keep a Changelog](https://keepachangelog.com/) format) is the
single source of the GitHub release notes; the release type is explicit.

```
release.ps1 -Version <x.y.z> -Type <draft|prerelease|release> [-Stage Preview|Prepare|Publish] [-Force]
```

- **Preview** (default) — lists the version bump, the files that change, the working-tree changes
  that would be committed, the CHANGELOG notes and the plan. Changes nothing (the approval gate).
- **Prepare** — bumps the three version locations (`VersionInfo.txt`, `Properties\AssemblyInfo.cs`,
  `PluginVersion.cs`), builds, creates branch `release/vX.Y.Z`, commits, pushes and opens a PR.
  No GitHub release yet.
- **Publish** — run **after** the PR is merged to `main`: builds from `main` and creates the GitHub
  release `vX.Y.Z` (tag on `main`) with the `.plgx`/`.dll` assets and the CHANGELOG section as notes.

`-Type` maps to `--draft` / `--prerelease` / (none = a normal "Latest" release). GitHub shows a
SHA-256 digest per asset itself, so no `SHA256SUMS` file is shipped. `Prepare` generates the
`## [x.y.z]` section in `CHANGELOG.md` automatically from branch commits — review/edit it before
confirming. The umbrella **`..\release-all.ps1`** releases both plugins in lockstep at one version;
run a single repo's `release.ps1` to release one plugin.

### Development workflow per version

Feature work for a release follows a lightweight branch model — no per-feature branches:

1. **Create the release branch** — run `release.ps1 -Stage Preview` first (dry-run), then
   `release.ps1 -Stage Prepare` to bump the version, create `release/vX.Y.Z`, commit the bump,
   push, and open the PR against `main`.
2. **One commit per completed feature** — after each feature is done, Lars commits it directly on
   `release/vX.Y.Z` with a clear, scoped message (e.g. `add PGP fixture self-test`).
   The developer agent signals when a feature is ready by outputting a suggested commit message.
3. **PR accumulates feature commits** — the single open PR `release/vX.Y.Z → main` is the
   review surface for the whole version. No force-push; no squash during development.
4. **Publish on merge** — once Lars merges the PR to `main`, run `release.ps1 -Stage Publish`
   to tag `main` and create the GitHub release.

## Architecture

### Solution layout

```
src/
├── Shared/                        # No data-source-specific code; compiled INTO each plugin (no Shared.dll)
│   ├── Caching/                   # ILocalCache, CacheEntry, FileSystemJsonCache
│   ├── Http/                      # ConditionalHttpFetcher, FetchResult, FetchOutcome, UserAgent
│   ├── DomainMatching/            # DomainCandidateGenerator (walks up host to eTLD+1 via PSL)
│   ├── KeePassUi/                 # PluginSettingsStoreBase wrapping IPluginHost.CustomConfig
│   └── Pgp/                       # Generic OpenPGP signature verification (BCL-only)
└── KPPasskeyChecker/
    ├── KPPasskeyCheckerExt.cs     # Plugin entry point; column provider + Tools menu + UpdateUrl
    ├── Properties/AssemblyInfo.cs # AssemblyProduct "KeePass Plugin" (required) + version
    ├── Data/                      # Domain model, API client, PasskeyDirectoryService, PasskeyTrustAnchor
    ├── Settings/                  # PasskeySettingsStore, PasskeySettingsForm
    └── UI/                        # PasskeyColumnProvider (done; search/detail forms still planned)
```

Future plugins (e.g. `KP2faChecker`) get their own `src\<Plugin>\` project that links the same `Shared\` source — each compiles to its own single self-contained DLL/.plgx.

### Key data-flow concepts

- **PasskeyDirectoryService** owns the fetch-and-cache lifecycle. UI reads `PasskeyDirectoryService.Current` for cache status (last refreshed, staleness).
- **PasskeyApiClient** fetches one of `all.json`, `supported.json`, `passwordless.json`, or `mfa.json` from `https://passkeys-api.2fa.directory/v1/` depending on the configured `PasskeyDataScope`. Only the selected endpoint is ever fetched.
- **ConditionalHttpFetcher** issues `If-None-Match` conditional GETs using stored ETags. On failure it returns the last known-good cached payload so the plugin never fails hard. Response size is capped (`MaxResponseContentBufferSize`, 16 MB) to bound memory against a hostile endpoint — do not remove.
- **FileSystemJsonCache** stores content + metadata as two files per key under `%LocalAppData%\KeePassPluginCache\KPPasskeyChecker\`. Writes are atomic (`.tmp` then `File.Replace`).
- **DomainCandidateGenerator** walks from the full host down to the registrable domain (eTLD+1 per the Public Suffix List, including private PSL entries). The directory is checked at each candidate, most-specific first. Do not reduce straight to eTLD+1, and do not match only the raw host.
- **Settings** are persisted via `IPluginHost.CustomConfig` (`GetString`/`SetString`/`GetLong`/`SetLong`/`GetBool`/`SetBool`).

### API schema notes

- Fields `passwordless` and `mfa` take values `"allowed"` or `"required"`. Absence of a field means "not documented" — model as `nullable enum`, never as a third enum member.
- **Field semantics (verified):** `mfa` = the site supports passkeys/WebAuthn (incl. security keys) as a **second factor**; `passwordless` = passkeys can **replace the password** (primary login). `"allowed"` = optional, `"required"` = forced.
- **Confirmed doc bug:** the prose descriptions for `mfa`/`passwordless` at `passkeys.2fa.directory/api` are **swapped**. Trust the **endpoint names** (`mfa.json` / `passwordless.json`), which are correct — confirmed empirically (e.g. `wikipedia.org` is in `mfa.json` only: security key as 2FA for certain accounts, not passwordless).
- **Data source ≠ 1Password's `passkeys.directory`:** the plugin reads **2factorauth's** `passkeys-api.2fa.directory`. 1Password runs a *separate* `passkeys.directory` with different curation/metadata — don't conflate the two when cross-checking an entry.
- **Per-entry fields the v1 API actually returns** (verified live 2026-06-23 against `supported.json`, 378 entries; entry key = domain): `documentation` (320/378, passkey-setup URL); `mfa` (216) and `passwordless` (193) support flags (`allowed`/`required`); `regions` (139, country codes); `notes` (77, free text); `recovery` (21, account-recovery URL); and `url` (1, negligible — **not** currently mapped). **`regions` values can be exclusions**, e.g. `["-jp"]` = "all regions except JP". The v1 API does **not** return `contact`, `categories`, `img`, or `additional-domains` — the last is **pre-resolved into separate top-level domain keys**, so each alias (e.g. `amazon.ca`) is its own entry and matches automatically. (`PasskeyEntryMapper` still reads `contact`/`additional-domains` for forward-compat, but both are absent in current live data — a detail view should treat `Contact`/`AdditionalDomains` as effectively empty.) This is the full set a per-entry detail view can show.
- Attribution required wherever data is shown: *"Data sourced from Passkeys Directory by 2factorauth."* (CC BY 4.0).
- User-Agent format: `{PluginName}/{Version} (+{GitHubRepoURL})`.

### Signature verification (PGP)

Implemented in `Shared/Pgp/` (generic OpenPGP) + `Data/PasskeyTrustAnchor.cs` (the pinned 2fa key). Enabled by the `VerifyPgpSignature` setting.

- Each endpoint has a sibling `<file>.json.sig` served as `application/pgp-signature`. **Despite the `gpg --verify x.sig x` docs, it is NOT a detached signature** — it is a complete *inline* OpenPGP signed message (`gpg --sign` output): an old-format **Compressed Data packet (tag 8, ZIP/DEFLATE, first two bytes `A3 01`)** wrapping a one-pass-signature packet + a literal-data packet (which embeds the JSON) + a v4 signature packet. So verification decompresses, verifies the signature, and uses the **embedded** literal JSON — the bytes used are exactly the bytes signed (the embedded copy is byte-identical to the separately served `.json`).
- Algorithm is **RSA-4096 + SHA-512**, sig type `0x00` (binary). All native to .NET 4.8 (`System.Security.Cryptography.RSA.VerifyHash` with `RSASignaturePadding.Pkcs1`, `System.IO.Compression.DeflateStream`). **No BouncyCastle / external lib** — which matters because KeePass compiles plugin sources at load time with a fixed reference set (`System.IO.Compression` was added to the `.csproj` references for this).
- The DEFLATE inflate is **bounded to 16 MB** (decompression-bomb guard): a hostile `.sig` would otherwise expand to exhaust memory *before* the signature is ever checked. Keep the bound; verification is fully fail-closed (any malformed/oversized/invalid input returns an invalid result, never throws out).
- The signing key is published as a **DNS CERT record (type 37) on `security.2fa.directory`**. We **pin** it at build time in `PasskeyTrustAnchor` (full CERT RDATA hex) and assert its v4 fingerprint equals `0D504141CE290061BD4F95A4AD8483C1CBABC36D` (key id `AD8483C1CBABC36D`, UID *2FactorAuth (Code signing key) <security@2fa.directory>*). Verification only ever uses the pinned key — never key material from the message. Key rotation by 2factorauth requires a plugin update (intended fail-closed behaviour).
- When verification is on, the fetch path switches to the `.sig` URL and caches the **extracted verified JSON** under a separate cache key (`..._signed`), so toggling the setting can never serve unverified cached JSON as verified. On verification failure the plugin falls back only to previously *verified* cached data (or surfaces an error) — it never falls back to unverified `.json`.

### Update check

KeePass's built-in plugin update check (https://keepass.info/help/v2_dev/plg_index.html#upd) is wired up by overriding `Plugin.UpdateUrl` in `KPPasskeyCheckerExt` (returns `PluginVersion.UpdateUrl`).

- URL: `https://raw.githubusercontent.com/gusowski1/KPPasskeyChecker/main/VersionInfo.txt` — the `VersionInfo.txt` at the **repo root** on the `main` branch.
- File format (UTF-8, **no BOM**): a separator char on its own line, then `Title:Version` lines, then the separator again. `Title` must equal the **AssemblyTitle** (`KPPasskeyChecker`); `Version` is the **AssemblyFileVersion** (trailing `.0` may be omitted):
  ```
  :
  KPPasskeyChecker:0.1.0
  :
  ```
- **On every release**, bump three things together: `VersionInfo.txt`, `AssemblyVersion`/`AssemblyFileVersion` (in `Properties/AssemblyInfo.cs`), and `PluginVersion.Current`. The file lists the *latest available* version; listing the currently-installed version produces no update prompt.
- Not signed yet (relies on HTTPS to GitHub). KeePass supports signing the file (RSA/SHA-512 via `UpdateCheckEx.SetFileSigKey`) — deferred. The check only *notifies*; it never auto-installs.

### Settings (PasskeyDataScope)

| Key | Type | Default |
|-----|------|---------|
| `Scope` | `PasskeyDataScope` enum (`AnySupport` / `PasswordlessOnly` / `MfaOnly`) | `AnySupport` |
| `RefreshInterval` | hours | 24 |
| `VerifyPgpSignature` | bool | **true** |

`VerifyPgpSignature` (checkbox, **default on**) is **functional** — see *Signature verification (PGP)* above. When on (the default), the plugin downloads and verifies the `.sig` and uses the embedded signed JSON; the user can opt out in the settings dialog.

The settings dialog is opened from the Tools menu as a standalone dialog — not a tab inside KeePass's native Options dialog (not a stable plugin API surface).

## Conventions

- All code, identifiers, and comments must be in **English**.
- **User-facing strings are single-language** (English until localization; when localized, uniformly one language). The one exception is **OS-/framework-provided error messages** (e.g. a .NET `Exception.Message`): these are shown **verbatim and never translated**, so they appear in the user's OS language by design (the user is assumed to read their own OS language). A localized framework message shown next to our English text is therefore **intentional, not a defect** — do not "fix" it.
- Do **not** merge passkey and 2FA logic into one plugin.
- Licensed under **GPLv3** (`LICENSE`); any new dependency must be GPL-compatible (this is partly why the plugin sticks to the .NET BCL).
- Everything under `Libs\` except its `README.md` is **gitignored** — `KeePass.exe` must be supplied there locally for builds; see `Libs\README.md`.

<!-- Internal planning / backlog is kept in CLAUDE.local.md (gitignored, not committed). -->
