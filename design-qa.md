# Radial Menu Design QA

- source visual truth: local QA reference artifact (not committed)
- implementation screenshot: local QA artifact (not committed)
- full-view context: local QA artifact (not committed)
- edge-placement evidence: local QA artifact (not committed)
- normalized focused comparison: local QA artifact (not committed)
- viewport: 1406 x 933 application window; 520 x 520 overlay bound
- state: eight segments, segment zero selected, pointer placement, Output OFF,
  isolated recovery path

## Full-view comparison evidence

The Release app preserves the existing Instrument Workbench layout and adds one
Radial Menus workspace. The overlay is topmost without replacing or dimming the
foreground application. It uses the approved continuous wheel, external names,
small center, and solid black selected segment. When the editor was moved partly
off-screen, the 520 x 520 overlay began at active-monitor working-area left plus
16 pixels and remained fully visible.

## Focused comparison evidence

The normalized comparison places the approved wheel crop and implementation crop
in one image. The implementation matches the source hierarchy, monochrome palette,
eight-way geometry, outside labels, line-icon treatment, and release center. The
implementation center is intentionally smaller than the mock because the user
explicitly preferred the smaller center. Segment zero is centered at twelve
o'clock so its geometry matches the editor's clockwise ordering.

## Required fidelity surfaces

- Fonts and typography: both use a compact monospaced UI voice. External labels
  and the release caption remain readable at the implementation size. A one-pixel
  white halo protects labels over arbitrary foreground text.
- Spacing and layout rhythm: wheel, center, leaders, labels, and icons are evenly
  distributed. The 520-pixel bound gives all eight labels room without clipping.
- Colors and visual tokens: warm off-white, near-black outlines, and solid selected
  fill match the workbench and approved visual. No transparency-key fringe remains.
- Image quality and asset fidelity: visible symbols use the built-in Windows Segoe
  MDL2 icon library, not placeholder shapes or custom SVG approximations.
- Copy and content: the editor explains hold/turn/release, clockwise order,
  placement, and safe preview. Overlay labels come from user configuration.
- Accessibility and behavior: the overlay is non-activating, click-through, and
  Escape-cancelable. High selected contrast and label halos remain visible over
  light or text-heavy content. Keyboard traversal of the editor remains native.

## Findings

- [P3] Leaders are straight instead of elbowed.
  Location: external wheel labels.
  Evidence: the source uses short elbow leaders while the implementation uses
  radial leaders with a contrast under-stroke.
  Impact: minor stylistic difference; direction and label association remain clear.
  Follow-up: consider elbow leaders only if future long-label testing shows a
  scanning benefit.

## Open questions

- Mixed-DPI placement and all physical monitors still require a hardware/manual
  pass. The current active-monitor edge case was verified without changing system
  display settings.

## Comparison history

1. Initial pass found a P1 magenta fringe on leaders, labels, and wheel edges from
   antialiasing against the WinForms transparency key. Geometry/text rendering was
   changed to transparency-safe single-pixel rendering. The revised screenshot
   removed the fringe.
2. Second pass found P2 weak foreground separation and missing semantic visual
   cues. Label/leader contrast halos and built-in Windows action icons were added.
3. Final focused comparison found no actionable P0/P1/P2 difference. The remaining
   straight-leader variation is P3 and does not block the approved experience.

## Implementation checklist

- [x] Continuous 4/6/8 wheel geometry
- [x] External labels and recognizable icons
- [x] Compact release center and selected contrast
- [x] Pointer and active-monitor-center placement modes
- [x] Full-bound active-monitor clamping
- [x] Focusless/click-through presentation and Escape cancellation
- [x] Safe editor preview and isolated Release capture

final result: passed
