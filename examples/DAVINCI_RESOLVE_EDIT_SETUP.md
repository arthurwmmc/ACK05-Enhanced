# DaVinci Resolve Edit Profile Setup

Configuration: `davinci-resolve-edit.json`

This example implements the accepted map in
`docs/design/DAVINCI_RESOLVE_EDIT_KEYMAP.md`. It is selected only while
`Resolve.exe` is foreground. The default profile emits no output.

## Required Resolve keyboard customization

Before Live use, open DaVinci Resolve Keyboard Customization and create a preset
named `ACK05 Edit`. Verify the existing assignments and add these two reserved
keys:

| ACK05 output | Resolve semantic command |
|---|---|
| F13 | Trim Start to Playhead |
| F14 | Trim End to Playhead |

The profile otherwise assumes the DaVinci Resolve default preset:

| Output | Intended command |
|---|---|
| I / O | Mark In / Mark Out |
| Arrow Up / Down | Previous / Next edit |
| Arrow Left / Right | Previous / Next frame |
| Space | Play/Stop |
| V | Select nearest edit point |
| U | Cycle edit point type |
| Comma / Period | Trim or nudge left / right |
| F | Match Frame |
| F9 / F10 | Insert / Overwrite |
| Backspace / Delete | Delete leaving gap / Ripple Delete |
| A / B | Selection / Blade Edit Mode |
| N | Toggle Snapping |
| Ctrl+Z / Ctrl+Shift+Z | Undo / Redo |
| Ctrl+S | Save Project |
| Ctrl+- / Ctrl+= | Timeline zoom out / in |

Do not start Live until the current Resolve preset is checked. If a command differs,
change Resolve's `ACK05 Edit` preset rather than changing the physical grammar in
the JSON. Record any required semantic change in the design document.

## Validation

```powershell
dotnet run --project .\src\Ack05Enhanced.Probe -- validate-config .\examples\davinci-resolve-edit.json
```

Live output remains explicit and requires the documented raw-mode and SendInput
permissions. Test against a disposable timeline first.
