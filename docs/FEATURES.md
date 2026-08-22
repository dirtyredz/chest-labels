# FEATURES — Chest Labels

What the mod does. Status: ✅ shipped · 🔜 planned.

## Naming & display
- ✅ **Name a chest** in place from the chest window (pencil button → text field; Enter saves,
  Escape cancels). `TitleEditor` + `LabelStore`.
- ✅ **Title in the chest window**, injected into the panel's native header band with the game's
  font, gold, ornament, and divider. Tunable position/size via config.
- ✅ **Floating world label** shown when the mouse is over a chest — via the game's own nameplate
  banner (preferred) or a self-drawn plate. `HoverLabel`.
- ✅ **Interaction-banner name** — optionally show the label in the game's purple interaction plate,
  at the exact moment the interaction arrow appears.
- ✅ **Hide the interaction arrow** while a named chest's label is showing (optional).

## Accessibility
- ✅ **Screen-reader announcement** of chest names via a read-time `CustomName` overlay
  (MoonlightAccess). Modes: Off / label only / "Type and label". Never written to save.

## Persistence
- ✅ **Per-save JSON sidecar**, keyed by chest GUID; save-scoped and save-safe (game save untouched).
- ✅ **Robust store** — atomic writes (temp + move), corruption quarantine (`.corrupt-*`), length
  cap, newline flattening, case-insensitive keys.
- ✅ **Label pruned** when a chest is destroyed.
- ✅ **F9 reload** labels from disk (hand-edit the JSON without restarting).

## Configuration
- ✅ ~20 settings via `Config.Bind`, grouped for Mod Menu (chest window, hover label, renaming,
  screen reader, diagnostics), including nameplate tint, hover height/size/opacity, and UI
  layout offsets.
- ✅ Diagnostics toggles: verbose per-chest logging and a chest-screen layout dump.

## Quality
- ✅ **Native look** enforced (game font/palette/rounded sprites; procedural pencil + plate).
- ✅ **Defensive patches** — any failing feature logs once and stands down for the session.
- ✅ **Unit tests** over `LabelStore` (32 checks, no framework, no game needed).

## Planned
- 🔜 **v2** — distance fade / richer world-label styling.
- 🔜 **v3** — search across labels → cross-chest item locator.

_Living doc — refresh with /project-docs when it drifts._
