# BACKLOG — Chest Labels

Prioritized trough of deferred work and known issues. Most-useful-first within each tier.
Structural items seeded by the full review of **2026-08-22** (see
[../STRUCTURE.md](../STRUCTURE.md) → Structural debt).

## P0 — none

No correctness or ship-blocking issues found.

## P1 — structural (do before major new chest-screen / hover work)

- [ ] **Split `ChestPatches.cs` (~954 lines, over the 800 cap).** Extract UI out of the patch class:
  - `ChestHeaderPresenter.cs` — `ApplyHeader`, `ApplyNativeTitle`, `ApplyOverlayPlate`,
    `HideOverlayPlate`, `RestoreOrnament*`, `ResolveHost`.
  - `ChestPanelGeometry.cs` — the five `Dictionary<int,Vector2>` caches
    (`headerHomes`/`panelHeights`/`layoutInsets`/`ornamentHomes`/`lineHomes`) behind one
    per-panel state object with `Apply`/`Restore`. **Not** a standalone generic dictionary helper.
  - `UiDiagnostics.cs` — `DumpTree`, `DescribeCorners`, header-diagnostics dump.
  - Move `ChestLabelsPlugin.HeaderDisabledAfterError` into `ChestHeaderPresenter` (consistent with
    the other three feature-local stand-down flags).
  - Mechanical, but only verifiable in-game — do it as its own reviewed change and retest a chest
    open/rename/close in the game.
- [ ] **Split `HoverLabel.cs` (~660 lines).** Extract, in order of safety:
  - `GameNameplateView.cs` — nameplate reuse + tint cache/restore (already owns its static state).
  - `HoverLabelPlateView.cs` — own canvas build + `Reposition` + `ApplyHoverStyle` (camera
    resolution folds in here; do **not** extract a separate camera class — single caller).
  - `ChestInteractionSource.cs` — interaction-target reflection + raycast fallback; also becomes the
    home for `ShouldSuppressArrow` so the arrow patch stops depending on the view MonoBehaviour.
  - Higher risk (live per-frame state) — its own change, retest hover show/hide/reposition + tint
    restore in-game.

## P2 — structural (opportunistic; do when touching the area)

- [ ] **Consolidate `PlayerCursorInteractionScreen` reflection** (arrow widget in `ChestPatches`,
  interaction source in `HoverLabel`) into one accessor — easier to audit against game updates.
  Fold into `ChestInteractionSource` during the `HoverLabel` split.
- [ ] Revisit `GameFonts.Search` vs. `HeavyFont` duplication **only if** a third same-shaped
  by-name asset lookup appears (currently too divergent to share — not worth it yet).

## P2 — product (from the version roadmap)

- [ ] **v2** — distance fade / richer world-label styling.
- [ ] **v3** — search across labels → cross-chest item locator.

## Explicitly won't-do (recorded so they aren't reopened)

- Shared exception-guard abstraction for the ~7 `catch → log → disable feature` sites — an idiom,
  not duplication; a wrapper fights Harmony's `ref` control flow.
- DI/interface seam for `ChestLabelsPlugin.*` config statics — idiomatic BepInEx; Harmony statics
  have no injection point; the class that needs decoupling (`LabelStore`) already has it.
- Unify `PanelSprite`/`PencilIcon` under a base class — different pixel math, zero reuse.

_Living doc — refresh with /project-docs when it drifts._
