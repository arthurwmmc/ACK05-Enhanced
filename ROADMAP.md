# Roadmap

This roadmap separates preview-release work from later product expansion. `TODO.md`
remains the detailed implementation checklist; this file records sequencing and
scope boundaries.

## Preview release

The first distributable preview proves the current ACK05 editor and runtime without
expanding the device or action model.

- Choose a release version for the public MIT-licensed
  `46slv/ACK05-Enhanced` repository.
- Review the shareable example profile and remove private process names or paths.
- Re-run the Windows workflow for the eventual release commit. The initial
  public main push passed its clean build and 40/40 self-tests on 2026-07-21.
- Verify the current UI at 125% and 150% DPI and on a clean Windows account.
  The 200% maximized/compact-selector path passed on 2026-07-21.
- Exercise corrupt-draft and externally changed-config recovery paths.
- Recheck Output OFF/ON waiting, connected, error, and panic-release states.
- Produce the package, screenshots, hashes, release notes, and an accurate unsigned
  build notice.
- After the preview is public and its installation path is stable, publish a
  Japanese note article covering the motivation, safety boundaries, current
  capabilities, limitations, and public repository link.

The bundled USB receiver and device-attributable direct Bluetooth input remain
documented hardware/transport limitations, not implicit preview-release promises.

## Usability follow-up

- Layer names, duplicate layer, and layer/binding copy-paste.
- Default per-user configuration and startup load/save.
- Recent/Favorites in the Action Library.
- Clearer invalid-drop feedback for pulse-only roller targets.
- Combo timing diagnostics and strict-term warnings.
- Tray status and optional auto-start.

## Advanced bindings

- Double Tap and Tap + Hold slots after the current Tap & Hold contract is stable.
- `TO`, `OSL`, `LT`, `LM`, and true multi-tap `TT` behavior.
- Macro editing and additional safe logical actions.
- Import/export and profile sharing with explicit compatibility results.

## Long-term exploration

- Radial action menus with independently assigned activation/forward/backward inputs.
- A native portable device/keymap package.
- A second real-device adapter to validate the generic input boundary before Core
  contracts are generalized.
- MIDI/HTTP/OBS integrations only after an allowlist and release-safety model exists.

See `docs/design/RADIAL_ACTION_MENU_CONCEPT.md` for the concept-stage radial-menu
details. Long-term exploration is not part of the current implementation contract.
