# ACK05 Enhanced Configuration Format

Status: schema v3 implemented; schema v1 and v2 are migrated automatically

Date: 2026-07-16

## Purpose

This is the stable, OS-independent JSON contract for profiles, layers, bindings,
and logical actions. Device, driver, pairing, firmware, and HidHide settings are
not stored here.

## Canonical schema v3 example

```json
{
  "schemaVersion": 3,
  "defaultProfileId": "default",
  "profiles": [
    {
      "id": "default",
      "name": "Default",
      "tapHolds": [
        {
          "id": "th0",
          "name": "B / Shift",
          "tappingTermMs": 200,
          "interruptMode": "holdOnOtherKeyPress",
          "onTap": { "kind": "keyboardChord", "keys": ["b"] },
          "onHold": { "kind": "keyboardChord", "keys": ["leftShift"] }
        }
      ],
      "comboDefaults": { "termMs": 50 },
      "combos": [
        {
          "id": "combo0",
          "name": "K1 + K2 Enter",
          "controls": ["k1", "k2"],
          "layers": [0],
          "ordered": false,
          "trigger": "press",
          "release": "firstMember",
          "action": { "kind": "keyboardChord", "keys": ["enter"] }
        }
      ],
      "layers": [
        {
          "index": 0,
          "bindings": [
            { "control": "k1", "action": { "kind": "momentaryLayer", "targetLayer": 1 } },
            { "control": "k2", "action": { "kind": "tapHold", "tapHoldId": "th0" } }
          ]
        },
        {
          "index": 1,
          "bindings": [
            { "control": "k1", "action": { "kind": "transparent" } },
            { "control": "k2", "action": { "kind": "keyboardChord", "keys": ["b"] } }
          ]
        }
      ]
    }
  ]
}
```

This example keeps K1 as MO(1), while K2 taps B and holds Left Shift.

## Combo definitions

- `comboDefaults.termMs` is the profile recognition window, 20-200 ms; default 50 ms.
- A definition contains exactly two distinct K1-K10 `controls`. Roller pulses are rejected.
- `layers` contains one or more unique existing layer indices. The active layer is
  captured at the first member Down and selects eligible definitions.
- Omitted/null `termMs` uses the profile default; otherwise it is 20-200 ms.
- `ordered` requires the displayed control order when true.
- `trigger` is `press`, `tapOnly`, or `holdTogether`.
- `holdTermMs` is omitted for `press` and required at 50-2000 ms for the other modes.
- `release` is `firstMember` or `allMembers` for held Keyboard/MO output.
- Combo output allows `disabled`, `keyboardChord`, `mouseWheel`, `momentaryLayer`,
  and `toggleLayer`. Tap & Hold references and nested composites are rejected.
- Two definitions cannot use the same unordered pair on overlapping layer scopes.

## Root and profile rules

- `schemaVersion` must be `3`. Unknown future versions fail closed.
- `defaultProfileId` must identify an existing profile.
- At least one profile is required; IDs are unique case-insensitively.
- Profile IDs use lowercase ASCII letters, digits, `.`, `_`, and `-`.
- `applicationMatch.processName` is optional, path-free, case-insensitive, and
  treats the `.exe` suffix as optional.
- The old global `layerSwitch` field is not written in schema v3.

## Layers and controls

- Each profile has 2 to 8 layers. New configurations start with 4.
- Indices are contiguous from Layer 0.
- Higher numbered active layers take priority.
- `transparent` falls through to the next active lower layer.
- A missing binding is interpreted as `disabled` on Layer 0 and `transparent`
  on upper layers. The GUI writes explicit bindings for clarity.
- K1-K10 and both roller directions are assignable. No key is globally reserved.
- A control may appear at most once per layer.

## Actions

| Kind | Payload | Behavior |
|---|---|---|
| `disabled` | none | No output. |
| `transparent` | none | Fall through to the next active lower layer. |
| `keyboardChord` | `keys` | 1-8 unique logical keys; release order is reversed. |
| `mouseWheel` | `wheelSteps` | Integer -10..10 excluding zero. |
| `momentaryLayer` | `targetLayer` | `MO(n)`: activate while the physical key is held. |
| `toggleLayer` | `targetLayer` | `TG(n)`: toggle on each key-down or roller pulse. |
| `tapToggleHoldLayer` | `targetLayer`, `tapHoldThresholdMs` | Short solo tap toggles; hold/chord is momentary. |
| `tapHold` | `tapHoldId` | Resolve a reusable profile-owned Tap & Hold definition. |

Layer targets must exist and cannot be the binding's own layer. The tap/hold
threshold is 100-1000 ms, default 200 ms. Pulse-only roller directions may use
`toggleLayer`, but not momentary or tap/hold actions because they have no key-up.

The first v2 slice intentionally defers `TO`, `OSL`, `LT`, `LM`, and Vial's
multi-tap `TT` semantics.

## Tap & Hold definitions

- IDs are unique within a profile and use lowercase letters, digits, `.`, `_`, or `-`.
- `tappingTermMs` is 100-1000 ms; default 200 ms.
- `interruptMode` is `holdOnOtherKeyPress`, `holdOnOtherKeyTap`, or `timeoutOnly`.
- Tap allows Disabled, Keyboard Chord, Mouse Wheel, and TG(n).
- Hold allows Disabled, held Keyboard Chord, and MO(n).
- Nested Tap & Hold and Combo actions are rejected in this first schema-v3 slice.
- A referenced definition cannot be deleted, and a layer targeted by a definition
  cannot be removed until the slot is changed.

## Schema v2 compatibility

Loading schema v2 upgrades only the schema container to v3 and adds an empty
`tapHolds` array per profile. Existing bindings, including legacy
`tapToggleHoldLayer`, retain their exact behavior. Saving writes schema v3.

## Schema v1 migration

Loading schema v1 automatically produces schema v3 in memory:

- The old global Momentary control becomes `MO(1)` on Layer 0.
- Toggle becomes `TG(1)`.
- Tap Toggle / Hold Shift becomes `tapToggleHoldLayer` with its threshold.
- The same control is `transparent` on Layer 1.
- Existing Layer 0/1 bindings remain; Layers 2 and 3 are added as transparent.
- Save writes schema v3. Unknown extension fields are retained where applicable.

## Persistence and recovery

- Serialization validates before writing.
- Explicit GUI save uses a same-directory temporary file and atomic replacement
  when possible, retaining `<config>.bak` for an existing file.
- A dirty editor stores a debounced recovery envelope at
  `%LOCALAPPDATA%\ACK05 Enhanced\recovery-draft.json`. It is separate from the
  user's explicit-Save file and contains the working draft, explicit-Save
  baseline, optional source path and SHA-256 fingerprint, and UTC timestamp.
- Startup validates both serialized configurations before offering Restore or
  Discard. A corrupt envelope is renamed with a `recovery-draft.corrupt-*` suffix
  and the saved/default configuration is used.
- Restore keeps the latest draft and Save baseline but not session Undo/Redo.
  Returning to a clean baseline or explicitly saving removes the recovery draft.
- Before overwriting the original path, the editor compares its current SHA-256
  fingerprint with the loaded/saved fingerprint. An external change requires an
  explicit Overwrite, Save As, or Cancel decision.
- Valid live edits first release held synthetic keys, then replace the active
  profile/router configuration.
- Disconnect, profile change, configuration change, stop, and process shutdown
  reset active layers and release every held synthetic key.
