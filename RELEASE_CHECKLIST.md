# Release checklist

## Current preparation status (2026-07-21)

- Local Release build and the 40/40 deterministic self-test have passed.
- The public `46slv/ACK05-Enhanced` repository is connected and its first Windows
  GitHub Actions build/self-test run passed.
- Public visibility, the `ACK05-Enhanced` repository name, and MIT license are
  selected. The release version is not yet chosen.
- The maximized/compact-selector path passed at 200% Windows display scaling.
- Personal root/example working configs and backup files are ignored; intentionally
  shareable example profiles remain tracked.
- The checklist below stays unchecked until it is run against a release candidate.

## Scope and policy

- [x] GitHub owner and remote repository are connected; public visibility,
  repository name, and MIT license are recorded.
- [ ] Release version and supported Windows/.NET versions are recorded.
- [ ] No driver, HidHide, firmware, pairing, or undocumented device-write behavior was added.
- [ ] Known limitations and hardware verification status are accurate.

## Source hygiene

- [ ] `git status --short` contains no personal config or capture files.
- [ ] `ack05-config.json`, `.bak`, temp files, `bin/`, and `obj/` are ignored.
- [x] Shareable examples contain no private process names or device paths.
- [ ] README, SPEC, CHANGELOG, TEST_PLAN, INSTALL, and USER_GUIDE agree.

## Automated verification

- [x] `dotnet restore .\Ack05Enhanced.sln` succeeds.
- [x] Release build completes with zero warnings and errors.
- [x] Deterministic self-test reports the documented total.
- [x] GitHub Actions Windows workflow passes from a clean checkout.
- [ ] Schema v1/v2 migration and schema-v3 round-trip remain covered.

## Manual verification

- [ ] Start without a config and open/save/reopen a temporary config.
- [ ] Undo, Redo, Revert to Saved, and Save baseline behavior match the guide.
- [ ] Minimum window and 100/125/150% DPI remain usable; 200% is verified.
- [ ] Keyboard focus and tab navigation reach all essential controls.
- [ ] Output OFF and window close release every held synthetic key.
- [ ] K1+K2 Combo acceptance and three-key fail-closed behavior are hardware-tested.

## Package

- [ ] Publish output is produced from a clean tagged commit.
- [ ] Package contains the executable/runtime choice, INSTALL, USER_GUIDE,
  SECURITY, license, and release notes.
- [ ] Package starts on a clean supported Windows account.
- [ ] File hashes are recorded with the release.
- [ ] Signing status is stated accurately; unsigned builds are not presented as signed.

## Rollback

- [ ] Previous release remains available.
- [ ] Config backup and schema compatibility are documented.
- [ ] Users can switch Output OFF and return to the last saved configuration without
  changing drivers or device firmware.
