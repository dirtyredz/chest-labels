# GOTCHAS — Chest Labels

Non-obvious traps. Each: **trap → why → do instead.**

- **Don't write `ItemEntry.CustomName` to persist a name.** It's part of the default item-equality
  mask, so a named chest picked up into inventory stops stacking/selling/counting. → Use the sidecar
  (`LabelStore`); only *read-overlay* `CustomName` for screen readers, never write it.

- **`Camera.main` is null in this game.** The gameplay camera isn't tagged `MainCamera` (Cinemachine).
  → `HoverLabel.ResolveCamera` falls back to the highest-depth active screen camera; don't assume
  `Camera.main`.

- **The chest screen reads hotkeys through Rewired, not Unity's EventSystem.** So a text field with
  keyboard focus doesn't stop game hotkeys — typing "x" in a rename also reorders the chest. → While
  editing, `BasePlayerChestState.OnActiveUpdate` is prefixed to `false` and Escape is intercepted in
  `Plugin.Update` before the game closes the screen.

- **Escape mid-rename would close the whole chest screen.** → `Plugin.Update` catches Escape while
  `TitleEditor.IsEditing` and cancels the edit instead of falling through.

- **Panel geometry offsets accumulate if you don't cache the original.** Repeated chest opens would
  walk the ornament off the top. → Every nudge caches the original (keyed by `GetInstanceID()`) and
  recomputes from it; never add to the current value. (Debt: five separate caches — see
  [../STRUCTURE.md](../STRUCTURE.md).)

- **A font "donor" found with `includeInactive` may be hidden or fully transparent.** Copying its
  colour renders the label invisible with no error. → Borrow only the *font asset*; always set
  colour explicitly (`GamePalette`). Never leave `TMP_Settings.defaultFontAsset` in shipped code —
  it's a fallback, not a choice (this shipped broken once in v0.6.0).

- **`NameplateScreen` is shared with the game's own tooltips.** Tinting it leaks into the game's
  nameplates. → Cache every touched image's colour and restore it the instant the label goes away
  (`HoverLabel.ApplyNameplateTint`/`RestoreNameplateTint`).

- **Another mod's `NameplateScreen.Show` patch can throw on a stale type after a game update.** →
  A finalizer swallows only the stale-reference exception kinds (the base nameplate already
  rendered), so our label survives an outdated tooltip mod.

- **Don't suppress the hover label on "any screen showing."** `Energy`/`Mana`/interaction prompts
  are always in the show-stack, so that hides the label forever. → Positive gate:
  `PlayerCursorInteractionScreen.IsShowing` is up exactly when the player can point at the world.

- **Reflection field names can vanish on a game update.** → Every `AccessTools.Field` lookup is
  cached and, on miss, disables its feature and falls back — never throws.

- **`Directory.Build.props` and `pack.ps1` are workspace-synced canonicals.** Editing the copies here
  is overwritten by `../../tools/sync-mod-files.ps1`. → Edit the workspace originals.

- **Building deploys to the game's plugin folder** (the `DeployPlugin` target). → Pass
  `-p:SkipDeploy=true` when you just want to compile-check without touching the install.

- **This is a git worktree; hooks live in the common git dir.** The pre-push gate was installed to
  `.git/hooks/pre-push` in the main repo, so it covers every worktree.

_Living doc — refresh with /project-docs when it drifts._
