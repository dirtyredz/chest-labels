# STRUCTURE — Chest Labels

<!-- Last full review: 2026-08-22 -->

Where things live in the code, and where change is expected to land. Pairs with
[README.md](README.md) (design + decisions narrative) and [docs/](docs/) (the living-doc set).
For the workspace-wide picture see [../../STRUCTURE.md](../../STRUCTURE.md).

## Overview

A BepInEx 5 / HarmonyX plugin for the Unity Mono game *Moonlight Peaks* (netstandard2.1). It lets
a player name a storage chest and see that name in the open chest window, floating over the chest
in the world, in the game's interaction banner, and through screen readers. Names are stored in a
per-save JSON sidecar — **the game save is never written**.

Plugin sources are flat in `src/` (no `src/<ModName>/` nesting, per the workspace convention).
The one Unity-free piece (`LabelStore`) is exercised by a hand-rolled console test runner in
`tests/`.

## Architecture at a glance

Harmony patches hook the game's chest flow and inject/read labels; a store owns persistence; a set
of small UI helpers keep everything looking native. Data flow and hook points are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Components

| Component | Responsibility | Key files | Depends on | Seam (where change lands) |
|---|---|---|---|---|
| **Plugin bootstrap** | BepInEx entry, ~20 `Config.Bind` settings, hotkeys, store lifecycle | `src/Plugin.cs` | BepInEx, Harmony, `LabelStore` | add a setting / lifecycle change |
| **Chest patches + header UI** | Harmony patches for the chest flow **and** the in-panel title/overlay UI + panel geometry + diagnostics (⚠ God-file, see debt) | `src/ChestPatches.cs` | Harmony, game types, `GameFonts`, `GamePalette`, `TitleEditor`, `ChestIdentity`, `HoverLabel` | a new patch, or header layout tuning |
| **Hover label** | World-space label MonoBehaviour: target discovery, camera, own canvas, game-nameplate reuse/tint (⚠ large, see debt) | `src/HoverLabel.cs` | Unity, game types, `PanelSprite`, `GameFonts`, `ChestIdentity` | hover behaviour / rendering |
| **Rename editor** | In-place rename: pencil button + `TMP_InputField`, commit/cancel | `src/TitleEditor.cs` | Unity UI, `PencilIcon`, `HoverFeedback`, `GameFonts`, `LabelStore` | rename UX |
| **Label store** | Save-scoped JSON sidecar; normalize, cap, atomic write, corruption quarantine. **Unity-free** | `src/LabelStore.cs` | Newtonsoft.Json only | persistence format / rules |
| **Chest identity** | `Chest` → normalized label key (GUID, trimmed + lower-cased) | `src/ChestIdentity.cs` | game types | the "how to key a chest" rule |
| **Native-look helpers** | Game typeface + palette; procedural plate + pencil sprites; button feedback | `src/GameFonts.cs`, `GamePalette.cs`, `PanelSprite.cs`, `PencilIcon.cs`, `HoverFeedback.cs` | Unity / TMP | visual integration |
| **Tests** | Console runner over `LabelStore` (no framework) | `tests/Program.cs` | `LabelStore` | persistence coverage |

## Key flows

- **Open a chest** → `ChestScreen.OnShow` / `PlayerStorageChestState.OnActivate` patches → load
  store for the active save → look up label by chest GUID → inject the header band title.
- **Rename** → pencil button → `TitleEditor` swaps title for an input field → commit → `LabelStore.Set` + `Save`.
- **Hover a chest in the world** → `HoverLabel.Update` polls the game's interaction target (reflection)
  or raycasts → looks up the label → shows the game nameplate or a self-drawn plate.
- **Screen reader** → `GridObjectPersistence.CustomName` getter patch fills the empty name field
  with the label at read-time only (never written to save).
- **Delete a chest** → `Chest.Delete` prefix prunes the label.

## Conventions

- Plugin `.cs` flat in `src/`; version single-sourced from `src/ChestLabels.csproj` `<Version>` via
  `GenerateModBuildInfo` in `Directory.Build.props` — never hardcode a version in `Plugin.cs`.
- Every drawn element must look native: game font (`GameFonts.Apply`), sampled palette
  (`GamePalette`), rounded shapes (`PanelSprite`). See README "nothing ships looking bolted on".
- Every patch/`Update` body is wrapped defensively: on failure, log once and stand the feature
  down for the session rather than throwing per-frame or per-chest.
- `LabelStore` stays free of Unity/BepInEx types so the test project can run without the game.
- `Directory.Build.props` and `pack.ps1` are **workspace-synced canonicals** — edit the workspace
  originals under `../../tools/`, not the copies here.

## Where to find things

- "How is a chest identified / labels stored?" → `ChestIdentity.cs`, `LabelStore.cs`, `research/02-save-format.md`.
- "How does the header get into the chest window?" → `ChestPatches.cs` (`ApplyHeader`/`ApplyNativeTitle`).
- "Why reflection here?" → `ChestPatches.cs` (arrow widget), `HoverLabel.cs` (interaction source).
- "What settings exist?" → `Plugin.cs` (`Awake`).

## Structural debt

Findings from the full structural review of **2026-08-22** (componentization + abstraction lenses +
Codex cross-model). Fixed items are struck; the rest are tracked in
[docs/BACKLOG.md](docs/BACKLOG.md).

- **`ChestPatches.cs` is a God-file (~954 lines, over the 800 cap).** [P1] It conflates four
  concerns that change for different reasons: (1) Harmony patch glue, (2) in-panel header UI
  construction + panel geometry tuning, (3) the fallback overlay-plate UI, (4) recursive UI
  hierarchy diagnostics. Proposed seams: keep patch methods thin in `ChestPatches.cs`; extract
  `ChestHeaderPresenter.cs` (native title + overlay), `ChestPanelGeometry.cs` (the five
  instance-ID-keyed `Vector2` caches behind one per-panel state with `Apply`/`Restore`), and
  `UiDiagnostics.cs` (`DumpTree`/`DescribeCorners`/header dump). Mechanical but moves ~450 lines of
  UI code that is only verifiable in-game — backlogged, not done in the review pass.
- **`HoverLabel.cs` is a large MonoBehaviour (~660 lines).** [P1] Bundles polling/targeting,
  camera resolution, reflection-based interaction lookup, own-canvas UI, and game-nameplate
  reuse/tinting. Proposed seams: `GameNameplateView.cs` (nameplate reuse + tint cache/restore — it
  already owns its own static state, cleanest cut), `HoverLabelPlateView.cs` (own canvas + reposition),
  `ChestInteractionSource.cs` (interaction reflection + raycast fallback, shared with the arrow
  patch). Leave camera resolution in place (single caller — extracting would be premature). Higher
  risk (live per-frame state) — do as its own reviewed change with in-game retest.
- **Cross-file interaction-policy coupling.** [P2] `ChestPatches.PlayerCursorInteractionScreen_Update`
  calls `HoverLabel.ShouldSuppressArrow`, and both files independently reflect into
  `PlayerCursorInteractionScreen` internals. Consolidate into the proposed `ChestInteractionSource`
  when `HoverLabel` is split.
- **`ChestLabelsPlugin.HeaderDisabledAfterError` sits on the plugin**, unlike the three sibling
  "stand down after error" flags that live next to their feature in `ChestPatches`. [P2] Move it
  into `ChestHeaderPresenter` when that is extracted.
- **`GameFonts` has two near-duplicate asset-search passes** (`Search` vs. `HeavyFont`). [P2]
  Divergent enough (ordered-list + two asset types vs. single name) that a shared generic helper is
  *not* yet warranted — revisit only if a third same-shaped lookup appears.
- ~~**GUID normalization duplicated in three places.**~~ ✅ Fixed 2026-08-22 — extracted
  `ChestIdentity.GuidOf(Chest)`; `ChestPatches.GetChestGuid` and `HoverLabel.GetGuid` now delegate.
  (`LabelStore.Normalize` stays separate: it normalizes arbitrary strings incl. save GUIDs and must
  remain Unity-free.)
- ~~**Dead code: `FindFontDonor`/`FindFontDonorIn`.**~~ ✅ Fixed 2026-08-22 — removed (zero callers
  since font selection moved to `GameFonts`).

**Deliberately not changed** (considered and rejected in review, recorded so they are not
re-litigated):
- The repeated `catch → log → disable this feature for the session` guard (~7 sites) is a
  recognizable *idiom*, not duplication — the disabled flag's type/home varies per site, and a
  generic wrapper would fight Harmony's `ref __result`/`ref __exception` control flow. Leave as-is.
- Config accessed as ambient statics off `ChestLabelsPlugin.*` is the idiomatic BepInEx pattern;
  Harmony patch methods are static with no injection point, and the one class that benefits from
  decoupling (`LabelStore`) is already Unity- and plugin-free. No DI seam.
- `PanelSprite` / `PencilIcon` are correctly separate single-caller procedural generators; a shared
  base would add a hierarchy for zero reuse (different pixel math). Keep separate.

_Living doc — refresh with /project-docs when it drifts._
