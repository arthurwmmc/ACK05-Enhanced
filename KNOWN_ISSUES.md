# Known issues

## Resolved: compact-width workspace navigation overlap

Status: resolved and verified on 2026-07-21 at the current display scale

Verified on 2026-07-21 at the current display scale with the isolated Release
build resized to 1081 x 741.

The original behavior left `Combos` partially visible beneath the `•••` button and
hid `Profiles`. The fix now replaces all four workspace pills atomically with one
active-workspace selector when their navigation container becomes too narrow. Its
menu exposes and checks Keymap, Tap & Hold, Combos, and Profiles. The separate
`•••` file menu remains unchanged.

The physical map, selected-key inspector, Action Palette scrolling/resizing,
fixed status bar, and Output OFF state remained reachable at this size.

Verified at 1081 x 741 by navigating to Profiles and Combos through the compact
menu, then maximizing and confirming all four pills returned with Combos selected.

## DPI matrix partially verified

200% system scaling was verified on 2026-07-21 using the user's active Windows
display setting and the Release build. At the available maximized width, the
header correctly used the compact workspace selector. Keymap, Tap & Hold,
Combos, and Profiles remained reachable; the physical map, inspector, Action
Palette, K9 label scale, white/outlined Combo badges, and Output OFF boundary
remained usable.

125% and 150% remain unverified. Responsive mode uses the navigation container's
measured client/control widths rather than a form-width constant, but each scale
still needs real display evidence. No system setting, registry value, sign-out,
driver, HidHide, firmware, or pairing state was changed by the test.

The Radial Menus workspace and focusless overlay were added after the four-tab
compact-header verification. The current selector measures all five workspaces,
and default-size Release UI plus active-monitor edge clamping passed. A renewed
compact-width 125/150/200% pass with the fifth workspace and multi-monitor radial
placement remains open before release.
