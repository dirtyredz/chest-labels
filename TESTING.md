# Chest Labels — Testing Notes

## v0.3.0 — native panel title

The hierarchy dump settled the "can we expand the UI?" question. The chest panel already
has a title band:

```
ChestContainer (410x650)
  ├─ Background     410x650
  └─ SlotContainer  340x630          <- no layout group: children are safe to add
       ├─ Header    340x50           <- the band, at the top of the panel
       │    ├─ Ornament 180x50       <- decorative flourish
       │    └─ Line     340x4        <- the panel's own divider
       ├─ Layout    340x490  [GridLayoutGroup]   <- item grid, untouched
       └─ Footer    340x50
```

Two facts made a native title safe rather than risky:

1. **`SlotContainer` has no layout group.** Only the inner `Layout` does, and that is the
   item grid. Adding a child to `Header` cannot re-flow the slots.
2. **No mask anywhere in the panel**, so nothing gets clipped.

**What v0.3.0 does:** the label now goes *inside* `Header`, and the decorative `Ornament` is
hidden while a title is shown — they occupy the same 180×50 space, so only one appears at a
time. The `Line` divider is deliberately kept; it is what makes the title read as part of
the window rather than pasted over it.

`Display / UseNativeTitle` (default `true`) switches back to the old floating plate if the
native look isn't wanted. Switching either way restores the ornament correctly.

**Hover:** raised from 0.25 back to 0.8 — roughly half of the drop from 1.4 — and the light
background stays at `HoverBackgroundAlpha = 0.3`.


## Run 3 (v0.2.0) — nothing showed. Two real bugs, neither cosmetic.

The log told the story: the chest opened and the label resolved
(`Opened chest bd7e9591-… - "Light Wood"`), but **the diagnostics block never ran** and no
error was thrown.

### Bug 1 — patch ordering (the header)

```csharp
protected override void OnActivate() {
    chest = base.data.Get<Chest>(0);
    UIScreen<ChestScreen>.Instance.Show(chest);   // <- OnShow fires HERE
    ...
}                                                 // <- the OnActivate postfix runs after
```

`ChestScreen.OnShow` runs *inside* `PlayerStorageChestState.OnActivate`, so it always
executes **before** that method's postfix — and `EnsureStoreLoaded()` only lived in the
postfix. On the first chest opened in a session the store had no save loaded, `Get()`
returned null, and `ApplyHeader` took its "no label" early return before reaching the
diagnostics dump.

This also explains run 2: v0.1.1 only appeared to work because the chest had been opened
more than once, and the second open found a store loaded by the first. A latent ordering bug
that logging happened to hide.

**Fix:** `EnsureStoreLoaded()` is now called in `ChestScreen_OnShow` as well, making the two
patches order-independent. A "no header drawn: chest … has no label" line now logs that path
explicitly instead of returning in silence.

### Bug 2 — `Camera.main` is null (the hover)

```
[Warning:Chest Labels] Camera.main is null - hover labels cannot position themselves.
```

The gameplay camera isn't tagged `MainCamera`, which is normal for a Cinemachine setup. The
hover never had a chance to work.

**Fix:** `ResolveCamera()` tries `Camera.main`, then scans `Camera.allCameras` for the
highest-depth active camera that renders to the screen, caching the result and re-resolving
if it's torn down. It logs which camera it settled on.

Also added a one-off `Hover raycast working: N collider(s) under cursor…` line, so if the
hover still fails we learn immediately whether the ray reaches world geometry at all — which
separates a layer/collider problem from a label-lookup one.

## v0.2.1 — both fixes, installed


## Run 2 (v0.1.1) — header visible, placement poor

The colour fix worked: **"Light Wood" rendered**, centred at the very top of the screen.
Two problems reported: no background made it hard to read against the night scene, and it
floated far from the chest panel it describes.

The diagnostics dump paid for itself immediately — it gave the exact runtime layout:

```
screen root      : ChestScreen  1920x1080   canvas=ChestScreen  font=Gelica-Bold
screen children  :
  - UIScreenBackground   1920x1080
  - InventoryContainer   1250x408    <- player bag, left
  - ChestContainer        410x650    <- the chest's own panel, right
```

## v0.2.0 — what changed

**Header**
- Parents to **`ChestContainer`** (the chest's own 410×650 panel) instead of the screen root,
  so the label rides with the box it names. Falls back to `ChestListWidget`, then the screen
  root, if the layout ever changes.
- Sits on a **solid dark plate** (`#1C102E`, ~88% opaque) so it reads against any background.
- Straddles the panel's top edge by default (`HeaderOffsetY = 0`) — "just above, slightly
  inside", as requested.
- Text **auto-shrinks** between 12 and 26pt so long labels never overflow.

**World hover (new)**
- Mouse over a chest in the world and its label appears above it.
- Own screen-space overlay canvas at `sortingOrder 500` — independent of the game's UI, so
  it cannot disturb a game screen.
- `Physics.RaycastAll` with `QueryTriggerInteraction.Collide`, because `Interactable`
  exposes an `InteractionCollider` that is very likely a trigger (plain raycasts skip those).
- Hides itself whenever the chest screen is open, and re-polls every 80 ms rather than every
  frame; between polls it still tracks the camera so the label doesn't lag when panning.
- Only labelled chests get a hover — unnamed ones stay quiet.

**Config note:** `HeaderOffsetY` changed meaning (was offset from the screen top, now from
the chest panel's top edge). BepInEx preserves existing values, so the stale `-24` was reset
to `0` in the `.cfg` directly — otherwise the new anchor would have been misplaced.


## Run 1 (v0.1.0) — everything worked except the header

Verified from `LogOutput.log` and the save file:

| Check | Result |
|---|---|
| Plugin loaded, patches applied | ✅ no errors or exceptions |
| Save GUID detected at runtime | ✅ `3de42d85-…` — matched the offline read exactly |
| Chest GUID read at runtime | ✅ `bd7e9591-c12a-a9df-791a-cbb4c6fb89e6` — exact match |
| Label resolved | ✅ `Opened chest bd7e9591-… - "Light Wood"` |
| Save still parses, uncorrupted | ✅ |
| **Nothing written to the game save** | ✅ `CustomName` occurrences: 0 |
| Sidecar left untouched when nothing changed | ✅ |
| **Header visible on screen** | ❌ **nothing appeared** |

Everything except the UI is confirmed working against a real chest.

### Why the header was invisible

A bug in `ApplyHeader`, found by re-reading the code rather than guessing:

```csharp
var donor = screen.GetComponentInChildren<TextMeshProUGUI>(true);  // includeInactive: true
text.color = donor.color;   // <-- copies the donor's colour
```

The donor is whatever text element turns up first **including inactive ones**, so it can
easily be a hidden or fully transparent element. Inheriting its colour makes the label
invisible — present in the hierarchy, rendering nothing, throwing no exception. Which is
exactly the observed symptom.

## v0.1.1 — the fix, plus diagnostics

- **Colour is now set explicitly** (opaque white with a dark outline) and never inherited.
- Only the **font asset** is borrowed from the donor; material and colour are not.
- Donor selection **prefers an active element**, falling back to inactive only if needed.
- Falls back to `TMP_Settings.defaultFontAsset` when no donor exists at all.
- An existing header with a null font or transparent colour is **repaired** rather than
  reused broken.
- New `LogUiDiagnostics` (default **on**) dumps the screen's canvas, font, colour, position,
  world corners and full child list every time the header draws.

Built and installed as `ChestLabels.dll` 19.5 KB.

### What to do next run

**Launch the game, open the chest, quit.** That's the whole job. Say whether a label
appeared; the log file is read directly from disk, so there is nothing to copy or paste.

The diagnostics block written to `BepInEx\LogOutput.log` answers the question either way:

```
  canvas           : NULL - text cannot render      <- wrong parent
  font             : NULL - text is invisible       <- no font resolved
  colour/alpha     : RGBA(…) alpha=0                <- still transparent somehow
  world corners    : bottomLeft=… topRight=…        <- off-screen or zero-sized
  screen children  : …                              <- what the screen actually contains
```

If it appears but sits in the wrong place, `Display / HeaderOffsetY` and
`Display / HeaderFontSize` are config values. They are read when the header is created, so
changing them needs a game restart rather than the F9 reload.

---

## Original v0.1.0 notes

## What's installed

| | |
|---|---|
| Plugin | `BepInEx\plugins\MoonlightPeaksMods\ChestLabels\ChestLabels.dll` (15.5 KB) |
| Config | `BepInEx\config\com.dirtyredz.moonlightpeaks.chestlabels.cfg` (created on first run) |
| Labels | `BepInEx\config\ChestLabels\3de42d85-….json` — **pre-seeded** with your chest |

The seeded label maps your real chest to **"Light Wood"**:

```json
{ "bd7e9591-c12a-a9df-791a-cbb4c6fb89e6": "Light Wood" }
```

## What to check

1. **Launch the game and load your save.** Nothing should look different yet.
2. **Open the chest with the light wood in it.**
   - Expected: **"Light Wood"** appears as a header at the top of the chest screen.
   - Expected in `BepInEx\LogOutput.log`:
     ```
     [Info : Chest Labels] Save 3de42d85-… : loaded 1 label(s) from …
     [Info : Chest Labels] Opened chest bd7e9591-c12a-a9df-791a-cbb4c6fb89e6 - "Light Wood"
     [Info : Chest Labels]     contents: 1 slot(s) in use
     ```
3. **Open house storage** (once the story reveals it). It has no label, so the log prints a
   copy-paste-ready line telling you exactly what to add to name it.
4. **Rename something.** Edit the JSON, then press **F9** in-game to reload — no restart.

## Honest status of each piece

| Piece | Confidence |
|---|---|
| Plugin loads, patches apply | **High** — all 5 targets verified present in `Vampire.Runtime.dll` |
| Chest GUID read + logged | **High** — `SerializedGuid.ToString()` confirmed to return the canonical dashed form matching the save file |
| Label storage / lookup | **High** — 16 tests, 32 checks, all passing |
| Label pruned when a chest is destroyed | **Medium** — logic is straightforward, but untested in-game |
| **Header text appears in the chest UI** | **Low–medium — the real unknown** |

### About that header

It's the one part that couldn't be tested without running the game. `ChestScreen` has no
title element, so the mod builds a `TextMeshProUGUI` from scratch and parents it to the
screen — using the same idiom ExtraTooltip uses, but the anchoring is an educated guess.

**Plausible outcomes:** it lands somewhere odd, gets clipped, or is hidden behind a layout
group. If injection *throws*, the mod catches it, logs the exception, disables the header for
the rest of the session, and keeps everything else working. **It will not break your chest
screen or your save.**

If the position is wrong, tell me where it landed and I'll adjust `anchoredPosition` /
`sizeDelta` in `ChestPatches.ApplyHeader`.

## Config options

`BepInEx\config\com.dirtyredz.moonlightpeaks.chestlabels.cfg`

| Setting | Default | Purpose |
|---|---|---|
| `Display / ShowHeader` | `true` | Turn the on-screen header off, keep logging |
| `Display / HeaderOffsetY` | `-24` | Vertical offset from the top of the chest screen |
| `Display / HeaderFontSize` | `32` | Header text size |
| `Diagnostics / VerboseLogging` | `true` | Log GUIDs + contents on open |
| `Diagnostics / LogUiDiagnostics` | `true` | Dump screen layout when the header draws |
| `Editing / ReloadKey` | `F9` | Re-read labels from disk |

New keys are appended to the `.cfg` on the next launch.

F9 was chosen to avoid known conflicts: F1 (Grimoire / ConfigurationManager), F5 (Save
Anywhere), F7/F8/F10 (Minimap).

## Not in this version

- **In-game rename UI.** Editing is via the JSON file for now. Building a text-input field
  needs study of `DirectorNameNewCreatureState`, which is the v0.2 job.
- **World-space floating labels.** Needs its own canvas — `NameplateScreen` is UI-space only.
- Shipping and treasure chests (separate states).

## Safety

- **Never writes to your game save.** Labels live only in the sidecar file.
- Deleting `ChestLabels.dll` removes the mod completely; the sidecar is inert JSON.
- A corrupt sidecar is renamed `.corrupt-<timestamp>` rather than deleted, and the mod
  carries on with an empty set.
- Every patch body is wrapped in try/catch that logs and continues.
- Your pre-modding save is backed up at `save-backup-2026-08-02/`.

## Rebuilding

```bash
dotnet build "mods/ChestLabels/src/ChestLabels.csproj" -c Release
```

Deploys to the plugins folder automatically. Tests:

```bash
dotnet run --project "mods/ChestLabels/tests/ChestLabels.Tests.csproj" -c Release
```
