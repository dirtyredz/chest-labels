# Research: Save File Format

Empirical findings from a real 2-day save, 2026-08-02. This resolves both blockers that were
holding up the ChestLabels design.

---

## Location

```
%USERPROFILE%\AppData\LocalLow\Little Chicken Game Company\Moonlight Peaks\
├── GlobalData.json            (+ .bak)      <- account-wide
└── <steam-id>\
    └── Saves\
        └── <save-guid>\
            ├── GameData.json      (+ .bak)  <- the world
            └── GameMetaData.json  (+ .bak)  <- slot summary
```

The game keeps a `.bak` of each file automatically — it rotates the previous save on write.

## Format: gzip, not JSON

Despite the `.json` extension, the files are **gzip streams** (magic `0x1F 0x8B`).

| | |
|---|---|
| On disk | 605 KB |
| Decompressed | 2,815,902 bytes (~2.8 MB) |

Decompress with `System.IO.Compression.GZipStream`. Any tool that reads the file as text
will get binary garbage — that's expected, not corruption.

### Serializer quirks

Written by **Newtonsoft.Json** with `PreserveReferencesHandling` on. Every object carries an
`$id`, and every collection is wrapped:

```json
"Rooms": { "$id": "44855", "$values": [ ... ] }
```

So paths are `Rooms.$values[0].GridObjects.$values[0]`, not `Rooms[0].GridObjects[0]`.

**Parse it with the game's own Newtonsoft**, which is sitting in the Managed folder:

```powershell
Add-Type -Path "...\Moonlight Peaks_Data\Managed\Newtonsoft.Json.dll"
$o = [Newtonsoft.Json.Linq.JObject]::Parse($text)
$o.SelectToken('Rooms.$values')
```

PowerShell 5.1's built-in `ConvertFrom-Json` **fails** on this file — it lowercases keys and
then trips over duplicate `keys`/`Keys`. Don't bother with it.

*(PowerShell gotcha: `if ($token)` on a `JToken` throws "String was not recognized as a
valid Boolean". Use `if ($null -ne $token)`.)*

---

## ✅ Blocker 1 SOLVED — save identity

The save directory name **is** the save GUID, and it's also stored inside the file:

```
Saves\3de42d85-edbc-bae3-1d57-0e3666fb43a7\   ==   GameData.json → "Guid"
```

Top-level fields relevant to identity:

| Field | Value in this save |
|---|---|
| `Guid` | `3de42d85-edbc-bae3-1d57-0e3666fb43a7` |
| `Version` | integer |
| `BuildVersion` | object |
| `TimeCreated` | date |
| `CurrentRoomInstanceGuid` | all-zeros when not in an instanced room |

**Sidecar path is settled:**

```
BepInEx/config/ChestLabels/<save-guid>.json
```

Steam ID scoping comes free — different accounts already get different save GUIDs.

---

## ✅ Blocker 2 SOLVED — GUIDs are globally unique

Measured across the whole save:

| | count | unique |
|---|---|---|
| Rooms | 27 | **27** |
| GridObjects | 3,759 | **3,759** |
| Inventories | 1 | 1 |

**Zero collisions across 3,759 objects in 27 rooms.** GridObject GUIDs are globally unique,
not per-room. The sidecar needs **no room key** — a flat `{ guid: label }` map is correct.

### The chest→inventory link is confirmed

The single `Inventory` record in this save has GUID `7a677ccb-…`, and there is exactly one
GridObject with that same GUID:

```json
{
  "ItemEntry": { "Amount": 1, "itemRef": { "SerializedGuid": "f49c22b3-2506-48f2-b080-a2ed5e4f530a" } },
  "Position": { "x": 36, "y": 88 },
  "Rotation": 2,
  "Guid": "7a677ccb-3fd3-fcd1-66b3-93fab37b870d"
}
```

This is exactly what `Chest.Load()` implied — the inventory is keyed by the grid object's
GUID. Confirmed against real data rather than inferred.

Note `Position` and `Guid` are siblings: **moving the chest changes `Position`, not `Guid`.**
Labels keyed by GUID survive the move. Proven, not assumed.

### That object is house storage, NOT a chest

**Corrected 2026-08-02** — the player confirmed they have not built a chest yet, and have not
even been shown house storage.

`f49c22b3-2506-48f2-b080-a2ed5e4f530a` is therefore **almost certainly the house-storage
ItemAsset**, not the chest one. Supporting evidence from the Addressables catalog:

```
fullgame_assets_..._content_housestorageintro_director.asset_...bundle
→ Content_HouseStorageIntro_Director.asset
```

House storage is **story-gated** — a director asset introduces it. The grid object is
pre-placed in the world from the start and simply not surfaced to the player yet, which is
why it appears in a 2-day save belonging to someone who's never seen it.

**Two things follow:**

1. **The GUID findings above are unaffected.** Uniqueness was measured across all 3,759 grid
   objects irrespective of type, and save identity is a top-level field. Blockers 1 and 2
   stand.
2. **The mod never needs an asset GUID anyway.** `PlayerStorageChestState.OnActivate` hands
   over a typed `Chest` instance (`base.data.Get<Chest>(0)`). Identifying chests by asset
   GUID is only relevant to offline save-file tooling, which this mod is not.

Useful bonus: **house storage uses the identical GridObject + Inventory + shared-GUID
pattern**, so the architecture generalizes cleanly across storage types.

---

## ✅ Verified against a real chest (second dump, 2026-08-03)

A chest was built in-game with light wood placed inside, then saved. Re-dumped and compared.

| | v1 (2 days) | v2 (chest built) |
|---|---|---|
| Decompressed | 2,815,902 B | 2,883,457 B |
| GridObjects | 3,759 (all unique) | 3,748 (all unique) |
| Inventories | 1 | **2** |

A second inventory appeared exactly as predicted. Its grid object:

```json
{
  "ItemEntry": { "Amount": 1, "itemRef": { "SerializedGuid": "22bca062-a80c-4952-aa65-fb181596c106" } },
  "Position": { "x": 40, "y": 85 },
  "Rotation": 2,
  "Guid": "bd7e9591-c12a-a9df-791a-cbb4c6fb89e6"
}
```

and the inventory under GUID `bd7e9591-…` holds one non-empty slot: `Amount: 7` of
`c984873a-c5df-4fa2-b3a0-e343fcdde934` — the light wood that was put in it.

### Confirmed asset IDs

| Asset | GUID | Count in world |
|---|---|---|
| **Chest** | `22bca062-a80c-4952-aa65-fb181596c106` | 1 |
| **House storage** | `f49c22b3-2506-48f2-b080-a2ed5e4f530a` | 1 |

Distinct, as the correction above predicted. The earlier n=1 object really was house storage.

### ⭐ GUID stability across a move — now observed, not inferred

The house-storage object appears in **both** dumps with the same GUID and a **different
position**:

| Dump | Guid | Position | Rotation | Asset |
|---|---|---|---|---|
| v1 | `7a677ccb-…` | **36, 88** | 2 | `f49c22b3…` |
| v2 | `7a677ccb-…` | **46, 95** | 2 | `f49c22b3…` |

Same identity, same rotation, same asset — moved. **`Position` changes while `Guid` does
not.** This was the central assumption of the whole ChestLabels design and it is now
empirically confirmed.

*Caveat: the cause of the move wasn't observed — it may have been the player relocating it
or the story's `Content_HouseStorageIntro_Director` placing it. Either way the GUID held
across a position change, which is the property that matters.*

### `CustomName` still absent

Occurrences in v2: **0**. Consistent with v1, and further evidence the field is dormant
unless deliberately written.

---

## Bonus: `CustomName` is confirmed unused here

```
occurrences of "CustomName" in the entire save: 0
```

`[DataMember(EmitDefaultValue = false)]` means it's omitted when null. Nothing in this save
has one yet — no animals named. This confirms the field is dormant unless deliberately set,
and reinforces the decision in [01-chest-system.md](01-chest-system.md) not to hijack it:
writing one would be the *only* `CustomName` in the file, and it would silently change that
chest's item identity everywhere the compare mask is used.

---

## Reproducing this

```powershell
$f = "$env:USERPROFILE\AppData\LocalLow\Little Chicken Game Company\Moonlight Peaks\<steam-id>\Saves\<save-guid>\GameData.json"
$bytes = [System.IO.File]::ReadAllBytes($f)
$in  = New-Object System.IO.MemoryStream(,$bytes)
$gz  = New-Object System.IO.Compression.GZipStream($in, [System.IO.Compression.CompressionMode]::Decompress)
$out = [System.IO.File]::Create("GameData.decompressed.json")
$gz.CopyTo($out); $gz.Dispose(); $out.Dispose(); $in.Dispose()
```

## Safety note

A copy of the pre-modding save sits at `save-backup-2026-08-02/` in this project directory.
It contains the account's Steam ID in its folder structure — **exclude it if these notes are
ever published or shared.**
