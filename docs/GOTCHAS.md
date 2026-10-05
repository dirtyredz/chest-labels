# GOTCHAS — Chest Labels

Non-obvious traps. Each: **trap → why → do instead.**

- **UI/geometry code has no automated tests.** → Only `LabelStore` is unit-tested; everything in
  `ChestPatches`/`ChestHeaderPresenter`/`ChestPanelGeometry`/`HoverLabel`/`TitleEditor` touches live
  Unity objects. → After any change there, **build Release to deploy and smoke-test in-game** (open a
  labeled chest; hover a chest). `dotnet build src/ChestLabels.csproj -c Release` copies the DLL into
  the game; `-p:SkipDeploy=true` builds without deploying.

- **`ChestScreen.OnShow` runs *inside* `PlayerStorageChestState.OnActivate`.** → It fires before
  `OnActivate`'s postfix, so relying on that postfix to load the store left the first chest of a
  session with an empty store and no header. → `OnShow` calls `EnsureStoreLoaded` itself.

- **The chest screen reads input through Rewired, not Unity's EventSystem.** → Rewired ignores UI
  keyboard focus, so typing a name containing "x" also triggered "reorder chest". → `OnActiveUpdate`
  is prefixed to return false while `TitleEditor.IsEditing`; Escape is intercepted in `Plugin.Update`
  so it cancels the edit instead of closing the chest.

- **`Camera.main` is null in this game.** → The gameplay camera isn't tagged `MainCamera`
  (Cinemachine). → `HoverLabel.ResolveCamera` scans active cameras for the highest-depth
  screen-rendering one.

- **The game's `NameplateScreen` is shared.** → Tinting it for our label would bleed into the game's
  own tooltips. → `HoverLabel` caches every touched image's original colour and restores it the moment
  the label hides; it also parks an invisible anchor so it never disturbs a nameplate shown for
  something else.

- **Never write the label into the game save.** → Writing `ItemEntry.CustomName` would affect item
  stacking/identity (the compare mask reads that field) and the save format. → The screen-reader
  feature only *returns* the label from the `CustomName` getter; the sidecar is the sole store. See
  `research/02-save-format.md`.

- **Another mod's broken patch can take down the shared nameplate.** → After game 1.2.3 renamed
  nameplate types, an outdated tooltip mod's patch threw and disabled our label. → A finalizer on
  `NameplateScreen.Show` swallows stale-reference exceptions (the base render already succeeded).

- **Reflected private fields can vanish on a game update.** → `showingSource`, `arrowWidget`,
  `source.Context` are looked up by name via `AccessTools`. → A lookup failure self-disables that
  feature and falls back (hover → raycast; arrow left alone), usually with a logged warning — though
  the missing-`Context` path sets the fallback flag silently. Check the BepInEx log after a game
  update if a feature stops.

- **Do NOT edit `pack.ps1` or `Directory.Build.props`.** → They're workspace-synced canonicals;
  local edits get overwritten. → Change build behavior at the workspace level.

- **Version lives in csproj `<Version>` only.** → It flows into `ModBuildInfo.Version` used by
  `Plugin.cs`; hardcoding a version there would double-source it. → Bump csproj only, and only when
  publishing.

- **Plugin `.cs` files are grouped by responsibility under `src/`** — `src/Plugin.cs` beside the
  `.csproj`, then `src/game/`, `src/ui/`, `src/core/`; never `src/ChestLabels/`. Docs + `pack.ps1`
  at mod root. Workspace convention; see STRUCTURE.md `## Layout` for the enforced contract.

- **Chest-window geometry depends on game panel names.** The chest-window geometry depends on the panel path `ChestContainer/SlotContainer/Header` and child names `Ornament`/`Line`/`Layout`. A game update renaming these degrades gracefully (overlay fallback / skipped nudges) but loses the native look — retest UI after each game update.

_Living doc — refresh with /project-docs when it drifts._
