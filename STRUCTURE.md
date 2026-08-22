# STRUCTURE — Chest Labels

Code-shape map for the Chest Labels mod. For *how the system works* see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); for *why* see [docs/DECISIONS.md](docs/DECISIONS.md).

_Last full review: 2026-08-22 (initial baseline)._

## Overview

A BepInEx 5 / HarmonyX plugin for the Unity/Mono game **Moonlight Peaks** (netstandard2.1). It lets
the player name storage chests and shows those names three ways: a title inside the chest window, a
floating label when hovering a chest in the world, and through the game's screen-reader name field.
Labels live in a per-save JSON sidecar and are **never written into the game save**.

All plugin `.cs` files are flat in [`src/`](src/) (workspace convention — no `src/ChestLabels/`).

## Architecture at a glance

```
Plugin.cs (BepInEx entry)
  ├─ binds config, owns LabelStore, presses F9 to reload
  ├─ Harmony.PatchAll(ChestPatches)          ← game hooks
  └─ AddComponent<HoverLabel>()              ← world hover (own MonoBehaviour)

ChestPatches (hooks) ─▶ ChestHeaderPresenter (chest-window title view)
                             ├─▶ ChestPanelGeometry (panel spacing state)
                             ├─▶ UiDiagnostics (layout dumps)
                             └─▶ TitleEditor (pencil rename)
HoverLabel (world label, orchestrator)
  ├─▶ ChestInteractionSource (which chest is pointed at)
  ├─▶ HoverLabelPlateView (mod's own plate)  ─▶ PanelSprite, GameFonts, GamePalette
  └─▶ GameNameplateView (game nameplate + tint)
TitleEditor ─▶ PencilIcon, HoverFeedback, GameFonts, GamePalette
everything data ─▶ LabelStore (Unity-free, unit-tested)
```

`ChestLabelsPlugin` is the hub: it installs the patches, spawns `HoverLabel`, and holds the shared
config/log/store that **every** component reads. So the shape is hub-and-spoke (Plugin ⇄ its
collaborators) — not a layered acyclic graph. Between the feature components themselves the arrows
run one way (hooks → presenter → geometry/diagnostics; views → sprite/font/palette helpers).

## Components

| Component | Responsibility | Key file | Exposes | Depends on | Seam |
|-----------|----------------|----------|---------|-----------|------|
| Plugin entry | Config binding, store lifecycle, patch install, reload/save | [src/Plugin.cs](src/Plugin.cs) | `ChestLabelsPlugin` (static config + `Store`/`Log`/`EnsureStoreLoaded`), `ScreenReaderNameMode` | LabelStore, ChestPatches, HoverLabel, TitleEditor | add a config entry / lifecycle hook |
| Game hooks | Thin Harmony patch bodies + chest→GUID lookup | [src/ChestPatches.cs](src/ChestPatches.cs) | (patches only; internal helpers) | ChestHeaderPresenter, HoverLabel, TitleEditor, LabelStore | add/adjust a game hook |
| Chest-window title | Draw the title into the panel header (or overlay fallback) | [src/ChestHeaderPresenter.cs](src/ChestHeaderPresenter.cs) | `ApplyHeader`, `HeaderDisabledAfterError` | ChestPanelGeometry, UiDiagnostics, TitleEditor, GameFonts, GamePalette | change how the title looks/places |
| Panel geometry | Per-panel spacing state (capture-once, restore) | [src/ChestPanelGeometry.cs](src/ChestPanelGeometry.cs) | `Apply`, `RestoreOrnament` | ChestLabelsPlugin config | tune header spacing |
| UI diagnostics | Read-only layout/geometry log dumps | [src/UiDiagnostics.cs](src/UiDiagnostics.cs) | `DumpPanelGeometry`, `DumpHeaderDiagnostics` | ChestLabelsPlugin.Log | add a diagnostic |
| World hover (orchestrator) | Poll loop, camera, canvas/anchor; wires the three below | [src/HoverLabel.cs](src/HoverLabel.cs) | `HoverLabel` (MonoBehaviour), `ShouldSuppressArrow` | ChestInteractionSource, HoverLabelPlateView, GameNameplateView | change the hover loop |
| Hover detection | Which chest is pointed at (interaction target ∥ raycast) | [src/ChestInteractionSource.cs](src/ChestInteractionSource.cs) | `FindChest`, `ShouldSuppressArrow`, `ShouldStandDown`, `GetGuid`, `UsingInteractionSource` | LabelStore | change detection |
| Hover plate view | The mod's own rounded plate + text | [src/HoverLabelPlateView.cs](src/HoverLabelPlateView.cs) | `Ensure`, `Show`, `Hide`, `Reposition` | PanelSprite, GameFonts, GamePalette | change the mod plate |
| Game nameplate view | Show via the game's nameplate; tint + restore | [src/GameNameplateView.cs](src/GameNameplateView.cs) | `Show`, `Hide`, `IsAvailable` | (game NameplateScreen) | change nameplate use |
| In-place rename | Pencil button + input field in the chest header | [src/TitleEditor.cs](src/TitleEditor.cs) | `Attach`, `IsEditing`, `Commit`, `Cancel` | PencilIcon, HoverFeedback, GameFonts, GamePalette, LabelStore | change the rename UX |
| Label store | Save-scoped JSON sidecar persistence (Unity-free) | [src/LabelStore.cs](src/LabelStore.cs) | `LabelStore` (`Get`/`Set`/`Remove`/`Save`/`LoadForSave`…) | Newtonsoft.Json only | change persistence/format |
| Fonts | Locate the game's Gelica font + outline material | [src/GameFonts.cs](src/GameFonts.cs) | `Apply`, `Font`, `HeavyFont`, `OutlineMaterial` | TMP | — |
| Palette | Game colour constants | [src/GamePalette.cs](src/GamePalette.cs) | colour fields | — | — |
| Plate sprite | Generate the 9-slice hover-label backing | [src/PanelSprite.cs](src/PanelSprite.cs) | `Get()` | — | — |
| Pencil sprite | Generate the pencil edit glyph at runtime | [src/PencilIcon.cs](src/PencilIcon.cs) | `Get()` | — | — |
| Button feedback | Hover/press scale+tint for the edit button | [src/HoverFeedback.cs](src/HoverFeedback.cs) | `HoverFeedback` (MonoBehaviour) | — | — |

`ModBuildInfo.Version` (used by `Plugin.cs`) is generated from the csproj `<Version>` by the
workspace build (see [docs/DECISIONS.md](docs/DECISIONS.md)); it is not a source file here.

## Key flows

- **Open chest → title.** `ChestScreen.OnShow` (patched) → `ChestHeaderPresenter.ApplyHeader` →
  `ChestPanelGeometry.Apply` makes room → title text built → `TitleEditor.Attach` adds the pencil.
- **Hover chest → label.** `HoverLabel.Update` polls the game's interaction target (falls back to a
  raycast) → looks up the label → shows it via the game's `NameplateScreen` (or the mod's own plate).
- **Rename.** Pencil click → `TitleEditor` swaps title for a `TMP_InputField` → Enter/deselect
  commits to `LabelStore` → sidecar saved.
- **Save lifecycle.** `EnsureStoreLoaded` points `LabelStore` at the active save GUID lazily; F9
  reloads from disk; `OnDestroy` flushes.

## Conventions

- Plugin `.cs` flat in `src/`; docs + `pack.ps1` at mod root.
- Version bumped in csproj `<Version>` only, only when publishing; never hardcoded in `Plugin.cs`.
- Commit identity `dirtyredz <dirtyredz@live.com>`.
- Substantive game-facing patches and UI-injection paths are wrapped in try/catch and stand down for
  the session on error rather than throwing every frame (a few trivial bodies are left bare).
- Do NOT edit `pack.ps1` or `Directory.Build.props` — workspace-synced canonicals.

## Where to find things

- **Add a setting:** `Plugin.cs` `Awake` (bind) + read `ChestLabelsPlugin.<Name>.Value` at use site.
- **Change a game hook:** `ChestPatches.cs`.
- **Change the chest-window title look:** `ChestHeaderPresenter.cs` (+ `ChestPanelGeometry.cs` for spacing).
- **Change the world hover:** `HoverLabel.cs`.
- **Change persistence:** `LabelStore.cs` (+ `tests/Program.cs` covers it).
- **Decompile notes:** `research/01-chest-system.md`, `research/02-save-format.md`.

## Structural debt

- **P2 — no automated coverage outside `LabelStore`.** All UI-geometry/detection code is verified
  only by in-game smoke test. See [docs/GOTCHAS.md](docs/GOTCHAS.md).
- **Nit — duplicated chest→GUID helper.** `ChestPatches.GetChestGuid` and
  `ChestInteractionSource.GetGuid` are identical 6-line helpers (trim + lowercase). Small and
  independent; centralize only if a third caller appears.
- _(Resolved 2026-08-22)_ The `ChestPatches` and `HoverLabel` God-files were both split by
  responsibility — no source file now exceeds the 800-line cap.
- **Nit — stale doc reference.** `GamePalette.cs` cites `10-visual-integration.md` "at the repo
  root"; no such file is in this repo (it's a workspace-level guide).

_Living doc — refresh with /project-docs when it drifts._
