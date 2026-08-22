# BACKLOG — Chest Labels

Prioritized trough of deferred work / known issues. P0 = do next, P1 = should, P2 = nice.

## P1
- [x] ~~**Split `HoverLabel.cs` by responsibility.**~~ Done 2026-08-22 → `HoverLabel` (orchestrator)
  + `ChestInteractionSource` (detection) + `HoverLabelPlateView` (mod plate) + `GameNameplateView`
  (game nameplate). 672 → 287 lines; all four under the cap.

## P2
- [ ] **Automated coverage beyond `LabelStore`.** Only `LabelStore` is unit-tested (`tests/Program.cs`,
  32 checks). Explore extracting more Unity-free logic (e.g. GUID normalization, label cleaning is
  already covered) that can be tested off a running game.
- [ ] **Fix stale doc reference** in `GamePalette.cs` — it cites `10-visual-integration.md` "at the
  repo root"; that file isn't in this repo (it's a workspace guide). Update or remove the pointer.
- [ ] **Verify estimated colours** in `GamePalette.cs` / `PanelSprite.cs` — several are estimated from
  screenshots, not sampled from shipped assets (flagged in-code). Re-check against the real nameplate.

## Known issues / watch-items
- The chest-window geometry depends on the panel path `ChestContainer/SlotContainer/Header` and child
  names `Ornament`/`Line`/`Layout`. A game update renaming these degrades gracefully (overlay
  fallback / skipped nudges) but loses the native look — retest UI after each game update.
- Reflection into `PlayerCursorInteractionScreen.showingSource` and `arrowWidget` can break on a game
  update; both self-disable and fall back (raycast / leave arrow alone) with a logged warning.

_Living doc — refresh with /project-docs when it drifts._
