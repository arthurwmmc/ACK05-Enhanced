# ACK05 Protocol Research

> Open Target is a host-side schema-v4 output action. It does not add or change
> any ACK05 HID command, report, transport claim, driver behavior, or raw-mode byte.

Research date: 2026-07-16

## Confirmed from public sources

### Device identity

- Vendor ID: `0x28BD` (Hanvon Ugee / XPPen)
- Product ID: `0x0202` (ACK05)
- The Linux kernel ACK05 HID-BPF implementation states that direct USB and the
  bundled receiver use the same PID.
- The open-source userspace driver selects usage page `0xFF0A`, usage `0x0001`.

### Official behavior

The official manual documents 10 shortcut keys, a roller with switch, four
customizable groups, and K1 as the group-cycle key when the driver is installed.
It supports wired USB, bundled receiver, and direct Bluetooth. The manual lists
Windows 10 or later for direct Bluetooth.

### Default report limitation

The Linux kernel source documents a standard keyboard report (Report ID `0x06`)
with fixed chords. It cannot always distinguish physical buttons: K8 and K10
both contain the `z` key value under different modifiers, and simultaneous key
slot assignment complicates physical identity.

### Raw bitmask mode

Both the Rust userspace implementation and Linux kernel source send the known
output report below to the vendor collection:

```text
02 B0 04 00 00 00 00 00 00 00 ...
```

The Linux kernel pads this to 32 bytes. The Rust implementation sends 10 bytes
through HIDAPI. The Windows probe pads to the collection's advertised output
report length and only sends it after explicit `--enable-bitmask`.

The reconnecting `watch --enable-bitmask` command preserves the same opt-in rule.
It sends the documented command once after each successful vendor collection
connection because raw mode cannot be assumed to survive a physical reconnect.

Raw input report layout observed by those implementations:

```text
byte 0: 0x02  vendor report ID
byte 1: 0xF0  raw pad report marker
byte 2: bit 0..7 = K1..K8 held state
byte 3: bit 0..1 = K9..K10 held state; bit 2 = roller center switch held state
byte 4..6: not used by current decoder
byte 7: 0x01 clockwise pulse, 0x02 counter-clockwise pulse
```

The ten key bits and roller-center bit are stateful, so press and release transitions can be derived by
comparing consecutive reports. Roller values are stateless pulses.

## Connection matrix

| Connection | Public evidence | v1 status |
|---|---|---|
| USB-C wired | Raw mode implemented in Rust and Linux kernel | Target, Windows verified for K1-K10/roller |
| Bundled USB receiver | Kernel says same PID; official support | Target, hardware unavailable |
| Direct Bluetooth | Officially supported; Rust driver explicitly panics | Enumerated on Windows; no vendor raw collection; deferred |

## Battery report

The Linux kernel source also recognizes raw battery marker `0xF2`, with charge
percentage and charging state in following bytes. Battery display is not required
for the first momentary-layer milestone.

## Evidence quality

- Official product behavior: confirmed by XPPen manual.
- USB raw mode and byte mapping: corroborated by two independent open sources,
  including upstream Linux kernel code.
- Windows wired access behavior: verified on hardware for the vendor collection.
- Direct Bluetooth enumeration: verified on hardware. A vendor raw collection was
  not exposed, and no device-attributable physical state report was obtained.

## Windows hardware observation: 2026-07-16

The connected unit matched `28BD:0202` and exposed a single vendor collection at
usage `FF0A:0001`, with both input and output report lengths equal to 32 bytes.
This directly confirms the collection-selection assumptions used by the probe.

Read-only monitoring produced periodic reports beginning `02 F2 01 3C 01`. The
known raw-mode output padded to 32 bytes produced a response beginning
`02 B0 04 01`, so Windows can open the collection for read/write and the device
recognizes the command.

The synchronized follow-up captures verified K1-K8 as byte 2 bits 0-7, K9-K10
as byte 3 bits 0-1, clockwise/roller-right as byte 7 value `0x01`, and
counter-clockwise/roller-left as byte 7 value `0x02`. Every key produced a held
report followed by the zero-state release report. Repeated identical held-state
reports observed for K8 did not create duplicate transitions.

Holding K1 while tapping K2-K10 produced the expected combined state masks, and
all routed events stayed on Layer 1 until K1 was released. Disconnecting USB while
K2 was held ended the input stream with a Windows device-disconnected error after
repeated held-state reports.

## Windows roller-center observation: 2026-07-21

With the same wired vendor collection and documented raw-mode command, five
roller-center presses consistently produced `02 F0 00 04 ...` while held and
`02 F0 00 00 ...` on release. No rotation or K1-K10 input was used during the
capture. This verifies byte 3 bit 2 as a stateful roller-center switch on the
tested Windows USB-C device; it is not an inference from Linux or Bluetooth.

## Windows direct Bluetooth observation: 2026-07-16

Direct Bluetooth retained `VID 28BD / PID 0202` but exposed four HID-over-GATT
standard collections: two mouse collections (`0001:0002`, 8-byte input), one
keyboard collection (`0001:0006`, 8-byte input), and one digitizer collection
(`000D:0002`, 10-byte input). All advertised zero output bytes. Unlike wired USB,
there was no `FF0A:0001` vendor collection.

Windows refused direct read-only handles to the four input-stack-managed
collections. A read-only Raw Input diagnostic confirmed that Windows registered
all four ACK05 sources. With `PenTablet.exe` running, synchronized controller
operations coincided with keyboard events that were not attributable to the
ACK05 Raw Input device, including events without a readable source handle. After
the user manually stopped `PenTablet.exe`, repeated K1, K2, and roller operations
produced no ACK05-attributable Raw Input event.

This verifies enumeration behavior only. It does not establish a physical key
mapping for direct Bluetooth, and no raw-mode command was sent over Bluetooth.

## GUI Live mode boundary: 2026-07-16

The Windows GUI uses the same verified vendor collection and the same padded
`02 B0 04 00...` command as the Probe CLI. `Start Live` is the explicit opt-in
for both that device write and Windows synthetic output. `Stop Live`, device
disconnect, profile change, and window close all run the existing reset and
panic-release path. No additional device command was introduced.

## Layer control implementation boundary: 2026-07-16

Momentary, Toggle, and Tap Toggle / Hold Shift are host-side interpretations of
one selected, already verified K1-K10 down/up bit. The selected control, mode,
and tap/hold threshold do not alter
the HID report format and introduce no new device write. Reset, disconnect, and
Live restart always clear a latched layer and restore Layer 0.

## Schema v2 multilayer boundary: 2026-07-16

Schema v2 changes only host-side interpretation. `MO(n)`, `TG(n)`, Tap Toggle /
Hold Layer, multiple active layers, and Transparent fall-through all consume the
same verified K1-K10 down/up and roller pulse reports. They introduce no new HID
command, firmware behavior, driver change, HidHide setting, or pairing change.

K1 is no longer globally reserved. The configured GUI/CLI pipeline first selects
the foreground profile, then resolves its 2-8 layer keymap. Pulse-only roller
directions may toggle a layer, but cannot perform momentary/tap-hold switching
because the hardware report provides no matching Up event.

## Schema v3 Tap & Hold boundary: 2026-07-17

Reusable Tap & Hold is entirely host-side. It consumes the same verified K1-K10
Down/Up transitions and introduces no HID command, firmware behavior, driver
change, HidHide setting, or pairing change. Roller directions remain ineligible
because their report is pulse-only. Timeout resolution is generated by a host
timer; reset discards pending taps and releases any confirmed Hold output.

## Schema v3 two-key Combo boundary: 2026-07-17

Combo recognition is entirely host-side and consumes only the already verified
K1-K10 Down/Up transitions. It introduces no HID command, firmware behavior,
driver change, HidHide setting, or pairing change. The host buffers two candidate
keys for a configurable 20-200 ms window, resolves them on its existing 10 ms
timer, and replays failed candidates to ordinary bindings. Reset discards pending
candidates and releases confirmed keyboard or momentary-layer output.

## Sources

- XPPen ACK05 manual: https://www.xp-pen.com/user-manual/ack05-wireless-shortcut-remote
- Linux userspace driver: https://github.com/MarSik/kymars-xppen-ack05
- Driver protocol module: https://github.com/MarSik/kymars-xppen-ack05/blob/main/src/xppen_hid/mod.rs
- Linux kernel HID-BPF program: https://github.com/torvalds/linux/blob/master/drivers/hid/bpf/progs/XPPen__ACK05.bpf.c
