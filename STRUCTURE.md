# STRUCTURE — Chest Labels

Code-shape map for the Chest Labels mod. For *how the system works* see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); for *why* see [docs/DECISIONS.md](docs/DECISIONS.md).

_Last full review: 2026-08-22 (initial baseline)._

## Overview

A BepInEx 5 / HarmonyX plugin for the Unity/Mono game **Moonlight Peaks** (netstandard2.1). It lets
the player name storage chests and shows those names three ways: a title inside the chest window, a
floating label when hovering a chest in the world, and through the game's screen-reader name field.
Labels live in a per-save JSON sidecar and are **never written into the game save**.

Plugin sources live under [`src/`](src/), foldered by responsibility (`game/`, `ui/`, `core/`) with
only the BepInEx entry point at the `src/` root — see [## Layout](#layout). There is no
`src/ChestLabels/` nesting level. All files share the single flat `namespace ChestLabels`; C# does
not tie namespaces to folders, so the folders are a code-shape map only and moving a file between
them never changes a type's name.

## Layout

```
ChestLabels/
├── pack.ps1                 # packaging script — workspace convention: lives at the mod root
├── STRUCTURE.md, CLAUDE.md, README.md, ...
├── docs/                    # the living-doc set (ARCHITECTURE, DECISIONS, FEATURES, ...)
├── research/                # decompile notes on the game's chest + save systems
├── scripts/                 # repo tooling — git hook installer + pre-commit formatter
├── screenshots/             # Nexus page art
├── tests/                   # ChestLabels.Tests — LabelStore coverage (compiles src/core/LabelStore.cs)
└── src/
    ├── ChestLabels.csproj
    ├── Plugin.cs            # BepInEx entry point — must sit beside the .csproj, not in a folder
    ├── game/                # interop with the live game
    │   ├── ChestPatches.cs           # Harmony patches on the storage-chest flow
    │   ├── ChestInteractionSource.cs # reads the game's live interaction target (reflection)
    │   ├── GameFonts.cs              # locates the game's Gelica font + outline material
    │   ├── GamePalette.cs            # the game's colour constants
    │   └── GameNameplateView.cs      # renders through the game's own NameplateScreen
    ├── ui/                  # the mod's own panels, widgets and generated art
    │   ├── ChestHeaderPresenter.cs   # draws the title into the chest window
    │   ├── ChestPanelGeometry.cs     # per-panel spacing state
    │   ├── HoverLabel.cs             # world hover-label orchestrator (MonoBehaviour)
    │   ├── HoverLabelPlateView.cs    # the mod's own hover plate
    │   ├── TitleEditor.cs            # in-place rename (pencil button + input field)
    │   ├── HoverFeedback.cs          # hover/press feedback for the edit button
    │   ├── PanelSprite.cs            # generated 9-slice plate sprite
    │   └── PencilIcon.cs             # generated pencil glyph
    └── core/                # the mod's own domain logic + diagnostics (Unity-light)
        ├── LabelStore.cs             # save-scoped JSON sidecar store (Unity-free, unit-tested)
        └── UiDiagnostics.cs          # read-only layout/geometry log dumps
```

**Enforced homes:**

- `src/game/` — Harmony patches and live-game bridges: anything that reads or intercepts the running game
- `src/ui/` — panels, widgets, presenters, views and runtime-generated sprites
- `src/core/` — the mod's own domain logic, state, persistence and diagnostics
- `src/Plugin.cs` — the BepInEx entry point; must sit beside the `.csproj` at the `src/` root
- `pack.ps1` — packaging script; workspace convention puts it at the mod root beside the docs
- `scripts/` — repo tooling: the git-hook installer and the pre-commit formatter
- `tests/` — the `ChestLabels.Tests` console project (`LabelStore` coverage)

Config binding lives in `Plugin.cs` rather than `core/` because BepInEx `ConfigEntry` binding is part
of the plugin lifecycle; the entry point is the only place that may reference BepInEx types directly.

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
| Game hooks | Thin Harmony patch bodies + chest→GUID lookup | [src/game/ChestPatches.cs](src/game/ChestPatches.cs) | (patches only; internal helpers) | ChestHeaderPresenter, HoverLabel, TitleEditor, LabelStore | add/adjust a game hook |
| Chest-window title | Draw the title into the panel header (or overlay fallback) | [src/ui/ChestHeaderPresenter.cs](src/ui/ChestHeaderPresenter.cs) | `ApplyHeader`, `HeaderDisabledAfterError` | ChestPanelGeometry, UiDiagnostics, TitleEditor, GameFonts, GamePalette | change how the title looks/places |
| Panel geometry | Per-panel spacing state (capture-once, restore) | [src/ui/ChestPanelGeometry.cs](src/ui/ChestPanelGeometry.cs) | `Apply`, `RestoreOrnament` | ChestLabelsPlugin config | tune header spacing |
| UI diagnostics | Read-only layout/geometry log dumps | [src/core/UiDiagnostics.cs](src/core/UiDiagnostics.cs) | `DumpPanelGeometry`, `DumpHeaderDiagnostics` | ChestLabelsPlugin.Log | add a diagnostic |
| World hover (orchestrator) | Poll loop, camera, canvas/anchor; wires the three below | [src/ui/HoverLabel.cs](src/ui/HoverLabel.cs) | `HoverLabel` (MonoBehaviour), `ShouldSuppressArrow` | ChestInteractionSource, HoverLabelPlateView, GameNameplateView | change the hover loop |
| Hover detection | Which chest is pointed at (interaction target ∥ raycast) | [src/game/ChestInteractionSource.cs](src/game/ChestInteractionSource.cs) | `FindChest`, `ShouldSuppressArrow`, `ShouldStandDown`, `GetGuid`, `UsingInteractionSource` | LabelStore | change detection |
| Hover plate view | The mod's own rounded plate + text | [src/ui/HoverLabelPlateView.cs](src/ui/HoverLabelPlateView.cs) | `Ensure`, `Show`, `Hide`, `Reposition` | PanelSprite, GameFonts, GamePalette | change the mod plate |
| Game nameplate view | Show via the game's nameplate; tint + restore | [src/game/GameNameplateView.cs](src/game/GameNameplateView.cs) | `Show`, `Hide`, `IsAvailable` | (game NameplateScreen) | change nameplate use |
| In-place rename | Pencil button + input field in the chest header | [src/ui/TitleEditor.cs](src/ui/TitleEditor.cs) | `Attach`, `IsEditing`, `Commit`, `Cancel` | PencilIcon, HoverFeedback, GameFonts, GamePalette, LabelStore | change the rename UX |
| Label store | Save-scoped JSON sidecar persistence (Unity-free) | [src/core/LabelStore.cs](src/core/LabelStore.cs) | `LabelStore` (`Get`/`Set`/`Remove`/`Save`/`LoadForSave`…) | Newtonsoft.Json only | change persistence/format |
| Fonts | Locate the game's Gelica font + outline material | [src/game/GameFonts.cs](src/game/GameFonts.cs) | `Apply`, `Font`, `HeavyFont`, `OutlineMaterial` | TMP | — |
| Palette | Game colour constants | [src/game/GamePalette.cs](src/game/GamePalette.cs) | colour fields | — | — |
| Plate sprite | Generate the 9-slice hover-label backing | [src/ui/PanelSprite.cs](src/ui/PanelSprite.cs) | `Get()` | — | — |
| Pencil sprite | Generate the pencil edit glyph at runtime | [src/ui/PencilIcon.cs](src/ui/PencilIcon.cs) | `Get()` | — | — |
| Button feedback | Hover/press scale+tint for the edit button | [src/ui/HoverFeedback.cs](src/ui/HoverFeedback.cs) | `HoverFeedback` (MonoBehaviour) | — | — |

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

- Plugin `.cs` under `src/`, foldered `game/` · `ui/` · `core/` (see [## Layout](#layout)); only
  `Plugin.cs` sits at the `src/` root. Docs + `pack.ps1` at mod root.
- One flat `namespace ChestLabels` regardless of folder — folders map responsibility, not namespaces.
- Version bumped in csproj `<Version>` only, only when publishing; never hardcoded in `Plugin.cs`.
- Commit identity `dirtyredz <dirtyredz@live.com>`.
- Substantive game-facing patches and UI-injection paths are wrapped in try/catch and stand down for
  the session on error rather than throwing every frame (a few trivial bodies are left bare).
- Do NOT edit `pack.ps1` or `Directory.Build.props` — workspace-synced canonicals.

## Where to find things

- **Add a setting:** `Plugin.cs` `Awake` (bind) + read `ChestLabelsPlugin.<Name>.Value` at use site.
- **Change a game hook:** `src/game/ChestPatches.cs`.
- **Change the chest-window title look:** `src/ui/ChestHeaderPresenter.cs`
  (+ `src/ui/ChestPanelGeometry.cs` for spacing).
- **Change the world hover:** `src/ui/HoverLabel.cs`.
- **Change persistence:** `src/core/LabelStore.cs` (+ `tests/Program.cs` covers it — note
  `tests/ChestLabels.Tests.csproj` compiles that file by explicit path, so moving it means editing
  the test csproj too).
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
