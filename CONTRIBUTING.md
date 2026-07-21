# Contributing to ACK05 Enhanced

Thank you for helping improve ACK05 Enhanced. Bug reports, hardware observations,
documentation fixes, and focused pull requests are welcome.

## Development setup

- Windows 10 or 11 x64
- .NET 8 SDK
- A physical ACK05 is optional for Core and self-test work

Run the standard checks before opening a pull request:

```powershell
dotnet restore .\Ack05Enhanced.sln
dotnet build .\Ack05Enhanced.sln -c Release
dotnet run --project .\tests\Ack05Enhanced.SelfTest -c Release
```

## Safety requirements

- Enumeration and decoding must remain read-only by default.
- Send only device commands documented in
  `docs/research/ACK05_PROTOCOL.md`, and require explicit opt-in.
- Do not install or replace USB/HID drivers, configure HidHide, change firmware
  or pairing, or control the XPPen driver automatically.
- Never commit private HID captures, device paths, personal configurations, or
  local process names. Private captures belong under `captures/private/`.
- Every held synthetic key must receive a matching key-up on cancellation,
  disconnect, profile change, configuration replacement, and process shutdown.

## Architecture boundaries

- Keep `Ack05Enhanced.Core` independent of Windows APIs.
- Keep device transports behind interfaces so transport work does not change
  layer, Tap & Hold, or Combo semantics.
- Prefer built-in Windows and .NET APIs. New runtime dependencies need a recorded
  design rationale and maintenance plan.
- Preserve schema v1/v2 compatibility unless a migration is explicitly designed
  and documented.

## Documentation and evidence

Protocol handling, supported transports, layer semantics, output behavior, and
user-visible command changes should update the relevant documents in the same
pull request:

- `SPEC.md`
- `ARCHITECTURE.md`
- `docs/research/ACK05_PROTOCOL.md`
- `TEST_PLAN.md`
- `DECISIONS.md`
- `CHANGELOG.md`

Label hardware findings as verified, inferred, or unverified. Evidence from one
transport or operating system must not be generalized to another without a real
hardware check.

## Pull request scope

- Keep fixes, feature changes, and broad refactors separate when practical.
- Avoid unrelated formatting churn.
- Include the reason for the change, its user impact, verification performed,
  remaining manual checks, and rollback path where relevant.
- For late-stage UI or safety fixes, review persistence, Undo, output release,
  accessibility, responsive layout, and DPI behavior before merging.

Security-sensitive findings should follow [SECURITY.md](SECURITY.md) instead of
including private reproduction data in a public issue.
