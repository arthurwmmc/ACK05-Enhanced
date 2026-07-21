# Instrument Workbench UI QA Report

Date: 2026-07-21

Status: current implementation accepted for source preview

## Scope

This report records visual and interaction checks for the native Windows Forms
editor. The evaluated state used the default profile, Layer 0, K1 selected, and
current-layer Combo badges visible.

## Layout acceptance

- Product identity and workspace navigation remain visible in the header.
- Profile and layer controls form a separate context row.
- The ACK05-shaped physical map remains the primary editing surface.
- The selected-key inspector stays visible to the right of the map.
- The Action Palette remains scrollable and vertically resizable below the map.
- Compact widths replace all workspace pills atomically with one workspace menu.

## Visual acceptance

- Cascadia Mono supplies the intended sparse technical typography.
- The warm-gray canvas, near-white surfaces, black selected state, and thin gray
  outlines use a restrained monochrome palette.
- K7 and K9 derive label scale from their shorter dimension, matching square keys.
- Combo badges remain white with a black outline even when overlapping a selected
  black member key.
- Code-native controls remain sharp at supported scaling; no raster UI decoration
  is required.

## Interaction acceptance

- Physical-key selection and the right inspector stay synchronized.
- One action-card click updates the working draft without writing the JSON file.
- Undo, Redo, Save baseline, and Revert to Saved remain distinct operations.
- The selected action can be dragged onto another K1-K10 key.
- Keymap, Tap & Hold, Combos, and Profiles remain reachable at wide and compact
  widths.
- Keyboard traversal reaches owner-drawn navigation, physical keys, action cards,
  and Combo badges with a visible focus cue.

## Regression history

### Action Palette startup and surface rendering

The initial draggable palette exposed an STA startup requirement, custom rounded
corner artifacts, and a legacy tree layout that did not match the card-based
interaction. The entry point is now STA, custom backgrounds paint cleanly, and
the library uses searchable categories plus rounded action cards.

### Typography and physical-map hierarchy

The initial system font weakened the intended hierarchy, category scroll position
hid important groups, and the roller center exposed internal placeholder text.
The workbench now uses Cascadia Mono, keeps Common/Layers/Navigation first, and
removes internal placeholder copy.

### Non-square keys and Combo overlays

K9 previously enlarged its text because scale followed control width. A black
Combo badge could also merge with a selected member key. Type scale now follows
the shorter dimension, while badges use the light key surface and black outline.

## Remaining polish

- Add only approved logo/icon assets with clear redistribution rights.
- Add Recent/Favorites after the preview stability work.
- Complete 125% and 150% DPI checks.

No actionable P0, P1, or P2 UI differences remain for the current source preview.
