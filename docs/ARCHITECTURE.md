# ARCHITECTURE — Chest Labels

How the system works at runtime (the code layout is in [../STRUCTURE.md](../STRUCTURE.md)).

## System overview

The mod is a single BepInEx plugin DLL. At `Awake` it binds config, constructs the `LabelStore`,
`PatchAll`s the Harmony patches, and attaches the `HoverLabel` MonoBehaviour. From then on it is
event-driven: game callbacks (patched) and the per-frame `HoverLabel.Update` do the work. There is
no server, no threads — everything runs on Unity's main thread.

## Data model

- **Chest identity** = `Chest.GridObjectPersistence.Guid`, normalized (trim + lower-case) by
  `ChestIdentity.GuidOf`. Stable across a chest being picked up and re-placed (verified on a real
  save — see `research/02-save-format.md`).
- **Save identity** = the save's top-level `Guid` (`GamePersistence.Instance.Guid`), also its save
  folder name.
- **Storage** = one JSON sidecar per save at
  `BepInEx/config/ChestLabels/<save-guid>.json`, a flat `{ "<chest-guid>": "<label>" }` map.
  Labels are trimmed, newline-flattened, and capped at `LabelStore.MaxLabelLength` (48).

**The game save is never written.** Uninstalling leaves zero trace, so the mod is genuinely
save-safe. The screen-reader feature is a *read-time* overlay on the game's `CustomName` getter,
not a write.

## Key flows

1. **Store loading (lazy).** `ChestLabelsPlugin.EnsureStoreLoaded()` points the store at the active
   save's GUID on demand and short-circuits when unchanged, so it is cheap to call from every hook.
   It lives on the plugin (not a chest patch) because the hover label needs labels before any chest
   is ever opened.
2. **Header injection.** `ChestScreen.OnShow` (postfix) resolves the chest GUID, looks up the label,
   and builds a title inside the panel's own `SlotContainer/Header` band — reusing the game's
   ornament, divider, and font so it reads as native. A floating overlay plate is a fallback if the
   panel shape ever changes. Panel geometry (top padding, ornament/line nudges) is cached per panel
   and always recomputed from the captured original, never accumulated.
3. **Rename.** The pencil button (`TitleEditor`) swaps the title for a `TMP_InputField`. While it has
   focus, `BasePlayerChestState.OnActiveUpdate` is prefixed to `false` so the game's Rewired hotkeys
   don't fire as you type. Enter/deselect commits via `LabelStore.Set` + `Save`; Escape cancels
   (handled in `Plugin.Update` before the game can close the screen).
4. **World hover.** `HoverLabel` prefers the game's own interaction target (read by reflection off
   `PlayerCursorInteractionScreen.showingSource`) so the label appears/disappears exactly with the
   game's interaction arrow; it falls back to a physics raycast. It renders either through the game's
   `NameplateScreen` (tinted, then restored) or a self-drawn plate.
5. **Screen reader.** `GridObjectPersistence.CustomName` getter (postfix) fills the otherwise-empty
   name field with the label at read-time, so MoonlightAccess announces it. Never overrides a real
   game-set name.
6. **Prune.** `Chest.Delete` (prefix) removes the label before the persistence component is nulled.

## External interfaces

- **BepInEx** — `BaseUnityPlugin`, `Config.Bind` (Mod Menu / ConfigurationManager read the tags).
- **HarmonyX** — patches against `Vampire.Runtime` and `chicken-ui`/`chicken-utilities` types.
  Hook points were established by decompilation (`research/01-chest-system.md`).
- **Newtonsoft.Json** — sidecar (de)serialization; already shipped by the game.
- **TextMeshPro / Unity UI** — all rendered text and sprites.

## Design notes

- **Defensive isolation.** Every patch/`Update` body catches, logs once, and stands its feature down
  for the session — a mod that throws in a UI callback can wedge the screen.
- **Foreign-patch guard.** A finalizer on `NameplateScreen.Show` swallows stale-reference exceptions
  thrown by *other* mods' patches so our label still shows.
- **Reflection is cached and fail-safe.** Missing fields disable the dependent feature and fall back
  rather than throwing, so a game update degrades gracefully.

_Living doc — refresh with /project-docs when it drifts._
