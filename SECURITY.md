# Security and safety policy

## Reporting a problem

Do not publish a report that includes private HID captures, device paths, local
process names, or personal configuration files. Preserve the smallest reproducible
log locally and remove private data before sharing it. Until the public repository
exposes a private security-reporting route, share only a sanitized symptom summary
and request a private follow-up channel.

## Safety boundaries

- Device enumeration and decoding are read-only by default.
- Only the raw-mode command documented in `docs/research/ACK05_PROTOCOL.md` may be
  sent, and only after explicit opt-in.
- Driver installation/replacement, HidHide, firmware, pairing, and XPPen driver
  control are outside the application.
- Windows output requires an explicit GUI `Output ON` or CLI opt-in.
- Every held synthetic output must receive a matching release on cancellation,
  disconnect, profile/config replacement, and process shutdown.
- Private captures belong in `captures/private/` and must not be committed.
- Recovery drafts stay under Local App Data because configurations may include
  private foreground-process names. They must not be copied into release packages.

## Known residual risks

- An ungraceful process or OS termination can bypass normal key release.
- Vendor software may emit the original input in addition to this tool's output.
- Direct Bluetooth may not provide a source-attributable physical state stream.
- Unsigned development builds have not completed a release hardening review.

Security-sensitive behavior changes require deterministic cancellation tests and
an update to the safety documentation before release.
