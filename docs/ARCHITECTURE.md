# ARCHITECTURE — Chest Labels

How the system works at runtime. For the code map see [../STRUCTURE.md](../STRUCTURE.md).

## System overview

Chest Labels is a single BepInEx plugin DLL. At `Awake` it binds configuration, creates a
save-scoped `LabelStore`, installs Harmony patches against the game's chest/interaction flow, and
attaches a `HoverLabel` MonoBehaviour to its own plugin GameObject. From then on it reacts to game
events (chest opened, chest screen shown, chest deleted, interaction text/name read) and to the
player's mouse and hotkeys.

Two independent surfaces read the same label data:

1. **Harmony patch layer** (`ChestPatches`) — hooks game methods to inject the chest-window title,
   feed the screen-reader name field, override the interaction banner, hide the interaction arrow,
   and prune labels on delete.
2. **Own MonoBehaviour** (`HoverLabel`) — runs its own update loop for the world-space hover label.
   By default it renders through the game's shared `NameplateScreen`; its own overlay canvas is the
   fallback plate (and the anchor host), kept separate so the mod's plate can't wedge a game screen.

## Data model

- **Label:** `chest GUID → display string`. The GUID is the chest's persistent
  `GridObjectPersistence.Guid`, normalized to trimmed lowercase.
- **Store file:** `BepInEx/config/ChestLabels/<saveGuid>.json` — a flat JSON object
  (`{ "<chestGuid>": "<label>" }`), one file per game save, keyed by the save's GUID. Labels are
  trimmed, newline-flattened, and capped at 48 chars. Blank clears the entry.
- **Never in the game save.** The screen-reader integration only *returns* the label when the game
  reads its `CustomName` getter; it never writes `ItemEntry.CustomName`, so item stacking/identity
  (which compare the stored struct field) are untouched. See `research/02-save-format.md`.

## Key flows / sequences

**Chest window title**
`PlayerStorageChestState.OnActivate` (postfix, logs) and `ChestScreen.OnShow` (postfix, draws) fire
when a chest opens. `OnShow` calls `EnsureStoreLoaded` (the store must be ready here — `OnShow` runs
*inside* `OnActivate`, before that method's postfix), resolves the label, and calls
`ChestHeaderPresenter.ApplyHeader`. The presenter prefers the panel's own `SlotContainer/Header`
band: `ChestPanelGeometry.Apply` grows the panel and deepens the grid inset (so the grid keeps its
height), drops the band, raises the ornament, pushes the divider down; then the title text is built
and `TitleEditor.Attach` adds the pencil. If the band can't be found (a game update reshaped the
panel) it falls back to a floating overlay plate.

**World hover label**
`HoverLabel.Update` reads the game's current interaction target (`PlayerCursorInteractionScreen`'s
private `showingSource`, via reflection) each frame; if that yields no chest it also tries
`Physics.RaycastAll`. When the interaction lookup is *entirely* unavailable (reflection failed) it
throttles to a poll interval instead of running every frame. Sharing the game's detection keeps the
label and the interaction arrow closely in step (arrow suppression is derived from the same
interaction target). With a label found, it renders through the game's `NameplateScreen` (default,
so font/shape/animation match) or the mod's own generated plate. While the mod tints the shared
nameplate it caches and restores every touched image's colour.

**Rename**
Pencil button (`TitleEditor`) swaps the title for a `TMP_InputField`. While it has focus,
`BasePlayerChestState.OnActiveUpdate` is prefixed to return false so the chest screen's Rewired
hotkeys don't fire mid-typing. Enter or deselect commits to `LabelStore` and saves; Escape (handled
in `Plugin.Update`) cancels without closing the chest.

**Persistence lifecycle**
`EnsureStoreLoaded` lazily binds the store to the active save GUID (short-circuits when unchanged, so
it's cheap to call from both the patches and the hover loop). Writes go via a temp file moved into place, so an interrupted write never leaves a half-written
sidecar (it deletes-then-moves, so not a fully atomic replace).
F9 forces a re-read from disk; `OnDestroy` flushes pending edits. Corrupt files are quarantined with
a `.corrupt-<timestamp>` suffix, never deleted.

## External interfaces

- **BepInEx 5** — plugin host, config system (`Config.Bind`), logging.
- **HarmonyX (0Harmony)** — method patching. Section-title/label metadata in `ConfigDescription.Tags`
  is read by the in-game **Mod Menu** (display names only; `.cfg` keys are untouched).
- **Game assemblies** (`Vampire.Runtime`, `chicken-ui`, `chicken-utilities`) — `Chest`,
  `ChestScreen`, `GridObjectPersistence`, `NameplateScreen`, `PlayerCursorInteractionScreen`,
  `UIScreen<>`, etc. Referenced with `<Private>false</Private>`; the game ships its own copies.
- **Unity** (`UnityEngine.*` modules) + **TextMeshPro** — UI construction, sprites, raycasting,
  legacy input; all referenced game-shipped (`<Private>false</Private>`).
- **Newtonsoft.Json** — sidecar (de)serialization (also game-shipped).
- **MoonlightAccess** (external screen reader) — consumes the `GridObjectPersistence.CustomName`
  getter the mod fills.

## Design notes

- **Defensive by default.** The substantive game-facing patches and UI-injection paths are
  try/caught and self-disable for the session on error — a mod that throws in a UI callback can wedge
  the screen. (A few trivial bodies are left bare: the one-line `OnActiveUpdate` prefix, the
  `NameplateScreen.Show` finalizer which handles the exception itself, and `HoverFeedback`'s pointer
  callbacks.)
- **Foreign-patch guard.** A finalizer on `NameplateScreen.Show` swallows stale-reference exceptions
  thrown by *other* mods' patches, so their breakage doesn't take the shared nameplate (and our
  label) down. Added in 1.0.1 after the game's 1.2.3 nameplate-type rename.
- **All art generated at runtime** (`PanelSprite`, `PencilIcon`) — the mod ships as one DLL with no
  asset bundle.

_Living doc — refresh with /project-docs when it drifts._
