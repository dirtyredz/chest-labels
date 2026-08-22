# Chest Labels

Name your chests, and see the name without opening them.

**Status:** 🚀 **Published** — v1.0.1 live on Nexus as
[mod 119](https://www.nexusmods.com/moonlightpeaks/mods/119), released 2026-08-03.

Source: <https://github.com/dirtyredz/chest-labels> (public, not linked from the Nexus page).
**Confirmed gap:** none of the 88 mods on Nexus (as of 2026-08-02) does chest naming.

For general modding setup see the root docs — especially
[03-dev-environment.md](../../03-dev-environment.md),
[04-first-mod-walkthrough.md](../../04-first-mod-walkthrough.md), and
[09-exploring-the-assembly.md](../../09-exploring-the-assembly.md).
This file covers only what's specific to this mod.

---

## Layout

See [STRUCTURE.md](STRUCTURE.md) for the full component map. In brief:

```
mods/ChestLabels/
├── README.md               <- this file: design, decisions, plan
├── STRUCTURE.md            <- code-shape map + structural debt
├── TESTING.md              <- what to try in-game, and what's uncertain
├── Directory.Build.props   <- shared build config (workspace-synced canonical)
├── pack.ps1                <- build + zip into Nexus layout (workspace-synced canonical)
├── docs/                   <- living docs: ARCHITECTURE, DECISIONS, FEATURES, GOTCHAS, BACKLOG
├── research/
│   ├── 01-chest-system.md  <- decompilation findings
│   └── 02-save-format.md   <- save file structure, measured on a real save
├── src/                    <- plugin sources, flat (netstandard2.1); no src/<ModName>/ nesting
│   ├── Plugin.cs           <- BepInEx entry point, config, F9 reload
│   ├── ChestPatches.cs     <- Harmony patches + in-panel header UI
│   ├── HoverLabel.cs       <- world-space hover label
│   ├── TitleEditor.cs      <- in-place rename (pencil + text field)
│   ├── LabelStore.cs       <- sidecar persistence (no Unity/BepInEx types)
│   ├── ChestIdentity.cs    <- Chest -> normalized label key
│   ├── GameFonts.cs · GamePalette.cs   <- native typeface + colours
│   ├── PanelSprite.cs · PencilIcon.cs  <- procedural sprites
│   └── HoverFeedback.cs    <- edit-button hover/press feedback
└── tests/                  <- console test runner (net8.0), no framework needed
```

---

## What research settled

Full detail in [research/01-chest-system.md](research/01-chest-system.md). The three
decisions that matter:

### ✅ Chest identity is a stable GUID

`Chest.GridObjectPersistence.Guid`, with `Position` stored as a separate field. Labels keyed
by GUID survive a chest being picked up and re-placed. **This was the make-or-break question
and it came out well.**

### ❌ Do not use the game's `ItemEntry.CustomName`

It exists, it persists, and it's how the game names animals — so it looks like free
persistence. But `CustomName` is part of `ItemEntryCompareMask.QualitySame_Metadata_CustomName`,
the default item-equality mask used across inventory, crafting, shops, and quest checks.
Since chests can be picked up into inventory, a named chest would stop matching unnamed
chests for stacking, selling, and "collect N chests" objectives.

Silent, save-corrupting-adjacent, and hard to diagnose. **Sidecar file instead.**

### ⚠️ The UI has to be built, not borrowed

`ChestScreen` holds only two `InventoryListWidget`s — no title element to repurpose. And the
game's `NameplateScreen` anchors to a `RectTransform`, so it's a UI-space tooltip, not
world-space floating text. Both the v1 header and the v2 world label are real UI work.

---

## Design

**Storage:** JSON sidecar via Newtonsoft.Json (already loaded by the game — nothing to ship).

```
BepInEx/config/ChestLabels/<save-guid>.json
{ "<chest-guid>": "Ores & Gems", ... }
```

Both parts of that path are now confirmed against a real save
([research/02-save-format.md](research/02-save-format.md)):

- `<save-guid>` is the save's folder name **and** its top-level `Guid` field. Steam-account
  scoping comes free, since different accounts get different save GUIDs.
- `<chest-guid>` is `GridObjectPersistence.Guid`, measured **globally unique across 3,759
  grid objects in 27 rooms** — so a flat map is correct and no room key is needed.
- `Position` is a sibling field of `Guid`, so moving a chest changes position and not
  identity. Proven on real data, not inferred.

**Never writes to the game save.** Uninstalling leaves zero trace, so the mod can honestly
claim **save-safe** — which this community reads for.

**Hook points:**

| What | Where |
|---|---|
| Chest opened | `PlayerStorageChestState.OnActivate` (Postfix) — gives the `Chest` instance |
| Chest closed | `PlayerStorageChestState.OnDeactivate` (Postfix) |
| Chest destroyed | `Chest.Delete` (Prefix) — prune the label |
| Screen shown | `ChestScreen.OnShow` (Postfix) — inject the header |

---

## Scope

| Version | Scope | New systems |
|---|---|---|
| **v1** | Label in the chest's open UI; rename via text field; sidecar JSON | UI + persistence |
| v2 | Floating world-space text above chests; config toggle; distance fade | world canvas |
| v3 | Search across labels → becomes the cross-chest item locator idea | search |

One new system per version. v1 alone is useful and shippable.

**Out of scope for v1:** shipping chests (`PlayerShippingChestState`) and treasure chests
(`PlayerTreasureChestState`) are separate states with separate screens.

---

## Blockers

1. ~~**Save-file identity.**~~ ✅ **Solved** — save GUID is the folder name and the top-level
   `Guid` field.
2. ~~**Chest GUID scope.**~~ ✅ **Solved** — globally unique, 3,759/3,759 with zero
   collisions. No room key needed.
3. **Text input.** `DirectorNameNewCreatureState` already collects a typed name from the
   player for animals — reuse that path instead of building a text field. **Open.**
4. **UI injection.** How `BaseUIScreen` builds its hierarchy, and where a header can be
   parented without fighting the layout. **Open — the main remaining unknown.**
5. ~~**Confirm the Chest `ItemAsset` GUID.**~~ ❌ **Dropped — not actually needed.**
   `PlayerStorageChestState.OnActivate` provides a typed `Chest` via `base.data.Get<Chest>(0)`,
   so the mod never identifies chests by asset GUID. (The one inventory in the test save
   turned out to be story-gated *house storage*, not a chest — see
   [research/02-save-format.md](research/02-save-format.md).)

Blockers 1 and 2 were the design-critical ones and both are closed. 3 and 4 are
implementation detail — they affect how much UI work v1 is, not whether the approach is
sound.

## ✅ Design verified end-to-end on a real chest (2026-08-03)

A chest was built in-game with light wood inside, then the save was re-dumped and compared.
Every assumption held:

| Assumption | Result |
|---|---|
| A chest is a GridObject with its own GUID | ✅ `bd7e9591-c12a-a9df-791a-cbb4c6fb89e6` |
| Its inventory is keyed by that same GUID | ✅ exact match, holding the 7 light wood |
| Chest asset ≠ house-storage asset | ✅ `22bca062-…` vs `f49c22b3-…` |
| GUIDs stay unique as the world changes | ✅ 3,748/3,748 unique after churn |
| **`Position` changes but `Guid` does not** | ✅ **observed** — house storage moved (36,88)→(46,95), GUID unchanged |
| `CustomName` stays untouched | ✅ 0 occurrences across both dumps |

The last row is the important one. Chest identity surviving relocation was the assumption
the entire sidecar design rests on, and it's now measured rather than reasoned about.

**There is nothing left to verify before writing code.**

---

## Design principle: nothing ships looking bolted on

**Anything this mod draws should be indistinguishable from something the developers built.**
If a player can tell which pixels are modded, that is a defect — not a polish item to get to
later.

This is written down because it was violated in v0.6.0, which shipped to Nexus with the hover
label rendering in Unity's stock TMP font while the in-panel title correctly used the game's
Gelica. The title borrowed its font from a neighbouring text element; the hover label lives on
the mod's own canvas with no neighbour to copy, so it silently fell back to the default and
nobody checked.

### The rule

Before any new UI element is considered done:

1. **Font** — use the game's own. `GameFonts.Apply()` resolves Gelica and its outline preset.
   Never leave `TMP_Settings.defaultFontAsset` in shipped code; it is a fallback, not a choice.
2. **Colour** — sample the game's palette, do not invent one. Deep plum `#1B0F2E`, muted gold
   `#C7A25B`, warm gold text `#F7D994`, matching the chest window and its item counts.
3. **Shape** — the game's panels are rounded with a lighter rim. Flat rectangles read as
   modded instantly. `PanelSprite` generates a 9-sliced rounded plate that matches.
4. **Reuse before generating.** The game ships material presets (`Gelica-Bold-Outline`,
   `Gelica-Bold-Glow`) and 1,659 UI icons. Check for an existing asset first; generate only
   when nothing suitable exists, as with the pencil.
5. **Ask the question explicitly:** *would a player know this was not in the base game?*
   Answer it for every element, not just the ones that look obviously wrong.

### Where this is easy to get wrong

An element parented **inside a game screen** tends to inherit the right look, because there is
something nearby to copy. An element on the **mod's own canvas** inherits nothing, so every
property is a decision — and the defaults are all wrong. Hover labels, floating text and
custom overlays need the most scrutiny, not the least.

## Conventions this mod will follow

- Settings via `Config.Bind` → free [Mod Menu](https://www.nexusmods.com/moonlightpeaks/mods/102)
  and ConfigurationManager support.
- **Not** F1 as a hotkey — collides with Serena's Grimoire and ConfigurationManager.
- Description states plainly: save-safe, comfort not cheat.
- UI strings through `LocalizationLibrary.Translate` rather than hardcoded English.

---

## Next session

Design is settled. Next:

1. **Install BepInEx** ([02-installing-mods.md](../../02-installing-mods.md)) and get the
   hello-world plugin loading. Prove the toolchain before writing chest logic.
2. Patch `PlayerStorageChestState.OnActivate` to log the chest GUID on open — smallest
   possible thing that proves the hook and the identity story end to end.
3. Once a chest is actually built in-game, open it and confirm the GUID logs — first
   observation of a real `Chest`.
4. Then tackle UI injection (blocker 4), which is the real work of v1.

Step 2 is the good milestone: once a real chest GUID prints to the BepInEx console, every
design assumption in this folder is verified live.

**Note:** steps 1 and 2 don't require having a chest — the plugin can load and the patch can
be in place before one exists. Only step 3 waits on materials.
