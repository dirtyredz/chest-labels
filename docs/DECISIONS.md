# DECISIONS — Chest Labels

Design decisions worth not re-litigating. Newest first. Rationale drawn from git history,
`CHANGELOG.md`, and code comments; where unknown it says so.

## 2026-08-22 — Split `HoverLabel` God-file into orchestrator + source + two views
Moved detection to `ChestInteractionSource`, the mod's own plate to `HoverLabelPlateView`, and the
game-nameplate + tint path to `GameNameplateView`; `HoverLabel` is now a thin MonoBehaviour
orchestrator (672 → 287 lines).
**Why:** the file mixed detection, camera, own-canvas rendering, and game-nameplate integration —
over the God-class cap and hard to change one concern without touching the others. **Rejected:**
folding camera resolution into the source (kept in the orchestrator, which owns the canvas and passes
the camera down). Removed the dead `ShowingLabel` flag (write-only; superseded by
`ShouldSuppressArrow` in 0.7.0).

## 2026-08-22 — Split `ChestPatches` God-file into presenter + geometry + diagnostics
Moved chest-window title drawing to `ChestHeaderPresenter`, the five per-panel `Dictionary<int,
Vector2>` spacing caches into one per-panel `ChestPanelGeometry` state object, and the layout dumps
into `UiDiagnostics`. `ChestPatches` is now thin Harmony bodies only (954 → 411 lines).
**Why:** the file was over the workspace 800-line God-class cap and mixed hooks with UI construction.
**Rejected:** a generic `Dictionary` helper for the caches (wanted a domain per-panel object, not a
map utility); reintroducing per-object identity keying inside the geometry object (accepted the
one-panel-key trade since the game reuses one stable chest panel).

## 2026-08-22 — Structure-review gate + living docs adopted
Installed the pre-push structure-review gate and this doc set.
**Why:** keep the mod's shape reviewed at each push. **Rejected:** relying on ad-hoc review.

## ~2026-08 — Source the plugin version from csproj `<Version>`
`Plugin.cs` reads `ModBuildInfo.Version` (generated from csproj) instead of a hardcoded string
(commit 4b8be6d).
**Why:** one source of truth; bump only when publishing. **Rejected:** a literal in `Plugin.cs`.

## 2026-08-20 (1.0.1) — Guard the shared game nameplate against foreign patches
A finalizer on `NameplateScreen.Show` swallows `TypeLoad`/`TypeInit`/`MissingMember` exceptions.
**Why:** game 1.2.3 renamed nameplate data types; an outdated *other* mod patching the same nameplate
would throw and disable our label for the session. The base nameplate is already drawn before
postfixes run, so the exception is safe to drop. **Rejected:** falling back to the self-drawn plate
(unnecessary — the game's render already succeeded).

## 2026-08-10 (1.0.0) — Screen-reader name via read-only `CustomName` overlay
Fill the game's `GridObjectPersistence.CustomName` getter result with the label at read time.
**Why:** MoonlightAccess reads that field and falls back to the asset type ("Storage Crate") when
empty. **Rejected:** writing the field into the save — would touch item stacking/identity and the
save format (see `research/02-save-format.md`); deliberately never written.

## 2026-08-03 (0.7.0) — Prefer the game's own nameplate/interaction target over a custom plate
Hover label renders through the game's `NameplateScreen`, and detection reads the game's interaction
target rather than a private raycast.
**Why:** exact visual match, and one shared source of truth so the label and interaction arrow never
disagree near the edge of range. **Rejected:** a hand-drawn plate as the default (kept as fallback
behind `UseGameNameplate=false`); the `Interactable.InteractionText` banner (matched the game but
moved the name to the bottom of the screen with a right-click icon — kept off by default behind
`UseGameInteractionLabel`).

## 2026-08-03 (0.6.0) — Per-save JSON sidecar keyed by chest GUID
Labels stored in `BepInEx/config/ChestLabels/<saveGuid>.json`, keyed by the chest's persistent GUID.
**Why:** survives pickup/replace; never risks the game save; easy to hand-edit. **Rejected:** writing
into the game save (identity/stacking risk); a single global file (would leak labels across saves).

## 2026-08-03 (0.6.0) — Suppress chest-screen hotkeys while renaming
`BasePlayerChestState.OnActiveUpdate` prefix returns false while the input field has focus.
**Why:** the screen reads input via Rewired, which ignores Unity's EventSystem focus, so typing "x"
would also reorder the chest. **Rejected:** nothing viable — Rewired doesn't honor UI focus.

## Undated — Generate all art at runtime
`PanelSprite` and `PencilIcon` build textures in code.
**Why:** ship one DLL, no asset bundle; the game has no pencil glyph (all 1,659 UI icons checked).

_Living doc — refresh with /project-docs when it drifts._
