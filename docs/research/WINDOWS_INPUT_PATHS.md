# Windows Input Path Research

Research date: 2026-07-16

## Option comparison

| Path | Can identify device | Physical key state | Suppresses original input | Decision |
|---|---:|---:|---:|---|
| AutoHotkey on normal key events | Weak | Ambiguous | No | Not core |
| Windows Raw Input on keyboard collection | Yes | Default chords, ambiguous buttons | No | Fallback diagnostic |
| Vendor HID bitmask collection | Yes | Yes, K1-K10 bit state | Not by itself | Adopted input |
| Firmware rewrite | Yes | Yes | Yes | Out of scope/risky |

## Windows APIs

Microsoft documents the normal userspace flow as:

1. SetupAPI enumerates HID collections.
2. `CreateFile` opens a collection.
3. `HidD_*` and `HidP_*` return attributes and report capabilities.
4. `ReadFile` reads input reports and `WriteFile` sends output reports.

The probe follows this model. Raw Input remains useful for distinguishing ordinary
keyboard sources but is not sufficient to recover physical identity in all ACK05
default chords.

## Output injection

`SendInput` is the planned v1 output. Microsoft notes that it is subject to UIPI:
an application can inject only into equal or lower integrity targets. It also does
not reset the existing keyboard state, so the action engine must pair every down
with up and maintain its own synthetic-key registry.

## Duplicate input

Switching ACK05 to vendor raw mode may stop the default keyboard report, but this
must be tested. If both physical/default and synthetic input reach applications,
HidHide can hide selected HID collections while whitelisting the feeder app.
HidHide is a system filter driver and may require reboot; it will remain an
optional manual setup, never an automatic first-run action.

## Why no third-party HID package yet

HIDAPI is mature and supports Windows, USB, and Bluetooth HID, but adding it would
introduce native packaging and license work. The initial probe uses built-in APIs
to test the actual Windows access boundary first. If native P/Invoke proves brittle,
HIDAPI is the leading fallback.

## Sources

- Microsoft Raw Input overview: https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input
- Microsoft opening HID collections: https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/opening-hid-collections
- Microsoft sending HID reports: https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/sending-hid-reports
- Microsoft SendInput: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
- HidHide repository and user guide: https://github.com/nefarius/HidHide
- HIDAPI: https://github.com/libusb/hidapi
