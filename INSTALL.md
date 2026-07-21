# Install and run

ACK05 Enhanced is currently a source-build preview for Windows 10/11 x64. A
signed installer is not available yet.

## Requirements

- Windows 10 or Windows 11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An ACK05 that Windows can expose through the vendor HID collection

Do not replace a driver, configure HidHide, change firmware, or re-pair the
device for this tool. The application does not perform those operations.

## Build and verify

From PowerShell in the repository root:

```powershell
dotnet restore .\Ack05Enhanced.sln
dotnet build .\Ack05Enhanced.sln -c Release --no-restore
dotnet run --project .\tests\Ack05Enhanced.SelfTest -c Release --no-build
```

The self-test does not require ACK05 hardware. A successful current run reports
`40/40 tests passed`.

## Start the editor

```powershell
dotnet run --project .\src\Ack05Enhanced.App -c Release
```

To open a known configuration on launch:

```powershell
dotnet run --project .\src\Ack05Enhanced.App -c Release -- .\examples\hardware-output-smoke.json
```

The editor starts with `Output OFF`. Editing the working draft does not access
the device. Switching to `Output ON` displays a confirmation before the documented ACK05
raw-mode command and Windows `SendInput` output can be used.

## Stop and recover

- Switch to `Output OFF` before closing when practical.
- Stop, disconnect, profile/config replacement, and normal process shutdown run
  the matching synthetic-key release path.
- If an unexpected output remains held after a process crash, press and release
  that physical modifier on a normal keyboard, then restart Windows if the state
  does not clear.
- The editor keeps a `.bak` beside an existing configuration when Save replaces
  it. Dirty working drafts are kept separately under Local App Data; startup
  offers Restore or Discard and always starts with output off.

## Current transport limits

- Direct Bluetooth source attribution is not established for every Windows path.
- The bundled USB receiver has not been hardware-tested in this project.
- XPPen input may still reach the foreground application; system-wide hiding is
  deliberately not configured automatically.
