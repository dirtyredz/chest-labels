# FEATURES — Chest Labels

What the mod does. Status: ✅ shipped · 🛠 in progress · 📋 planned.

## Naming
- ✅ **In-place rename** from the chest window — pencil button beside the title; Enter saves, Escape
  cancels (`TitleEditor`). Toggle: `ShowEditButton`.
- ✅ **Hand-edit + reload** — edit the JSON sidecar and press F9 (`ReloadKey`) to reload without
  restarting.
- ✅ **Label hygiene** — trimmed, newlines flattened, capped at 48 chars; blank clears the entry.

## Display
- ✅ **Chest-window title** — the label drawn into the panel's own header band under the game's
  flourish, in Gelica-Black gold (`ChestHeaderPresenter`). Toggle: `ShowHeader`. Tunable spacing:
  `HeaderFontSize`, `OrnamentOffsetY`, `TitleOffsetY`, `PanelTopPadding`, `HeaderDropY`, `LineOffsetY`.
- ✅ **Overlay-plate fallback** — a floating plate if a game update reshapes the panel and the header
  band can't be found (automatic, no setting).
- ✅ **World hover label** — shows a chest's name when the mouse is over it (`HoverLabel`). Toggle:
  `ShowHoverLabel`. Tunable: `HoverHeight`, `HoverFontSize`, `HoverBackgroundAlpha`.
  - ✅ **Via the game nameplate** (default) — uses the game's own speech-bubble banner. Toggle:
    `UseGameNameplate`. Recolour: `NameplateTint` (hex).
  - ✅ **Via the interaction banner** — feed the name into `Interactable.InteractionText`. Off by
    default (`UseGameInteractionLabel`); matches the game but repositions the name.
- ✅ **Hide the interaction arrow** while a named chest's label shows (`HideArrowWhenNamed`).

## Accessibility
- ✅ **Screen-reader name** — exposes the label through the game's `CustomName` field for readers like
  MoonlightAccess. Modes (`ScreenReaderName`): Type-and-label (default), Label-only, Off. Read-only;
  never written to the save.

## Persistence
- ✅ **Per-save JSON sidecar** keyed by chest GUID; temp-file-then-move writes (no half-written
  file); **never touches the game save**.
- ✅ **Label pruned on chest destroy**; **survives pickup/replace** (GUID is stable).
- ✅ **Corrupt-file quarantine** — an unreadable sidecar is moved to `.corrupt-<timestamp>`, not
  deleted; the session continues with an empty set.

## Integration / robustness
- ✅ **Game-native styling** — resolves the game's Gelica font + outline material (`GameFonts`);
  colours centralized (`GamePalette`); art generated at runtime (`PanelSprite`, `PencilIcon`).
- ✅ **In-game Mod Menu** section/label metadata on every setting.
- ✅ **Foreign-patch guard** — survives another mod's broken nameplate patch (since 1.0.1).
- ✅ **Self-disabling on error** — the substantive patches and UI-injection paths stand down for the
  session rather than throwing repeatedly.

## Diagnostics
- ✅ **Verbose logging** (`VerboseLogging`) — chest GUID + contents on open, on-screen UI stack.
- ✅ **UI layout dump** (`LogUiDiagnostics`) — chest-screen layout/geometry to the log
  (`UiDiagnostics`).

_Living doc — refresh with /project-docs when it drifts._
