# Research: The Chest System

Findings from decompiling `Vampire.Runtime.dll`, 2026-08-02. See
[../../09-exploring-the-assembly.md](../../09-exploring-the-assembly.md) for how to
reproduce any of this.

---

## 1. THE CRUX IS ANSWERED — chests have a stable GUID

This was the one question that decided whether the mod is a weekend or a slog.

```csharp
public class Chest : Interactable, IGridComponent, ICheckPlaceable
{
    public GridObjectPersistence GridObjectPersistence { get; private set; }
    public Inventory Inventory { get; private set; }

    public void Load(GridObjectPersistence gridObjectPersistence)
    {
        GridObjectPersistence = gridObjectPersistence;
        Inventory = new Inventory(GamePersistence.Instance.CurrentRoom.Inventories,
                                  gridObjectPersistence.Guid, out var newlyCreated, maxSize);
        ...
    }
}
```

And the persistence type:

```csharp
[DataContract]
public class GridObjectPersistence : GuidPersistence
{
    [DataMember] public ItemEntry ItemEntry;
    [DataMember] public Vector2Int Position;
    [DataMember(EmitDefaultValue = false)] public int Rotation;
    ...
}

[DataContract]
public class GuidPersistence
{
    [DataMember] public SerializedGuid Guid;
}
```

**`Position` is a separate field from `Guid`.** Identity does not derive from location, so a
label keyed by GUID survives the player picking up and re-placing a chest. This is the good
outcome — the position-keying nightmare is off the table.

The chest's inventory is *already* keyed by that same GUID, which confirms the GUID is the
game's own canonical chest identity.

---

## 2. The game already has `CustomName` — and it's a trap

`ItemEntry` carries a persisted custom name:

```csharp
[DataContract]
public struct ItemEntry
{
    [DataMember(EmitDefaultValue = false)] public string Metadata;
    [DataMember(EmitDefaultValue = false)] public string CustomName;
    ...
}
```

`GridObjectPersistence` even surfaces it: `public string CustomName => ItemEntry.CustomName;`

It's real and it works — the game uses it to let players **name their animals**
(`CreaturePersistence.SetName` → `ItemEntry.CustomName = name`, with UI in
`CreatureBuildingLedgerRenameTab` and `DirectorNameNewCreatureState`).

**Tempting: set `CustomName` on the chest and get persistence for free, save-native.**

### Why not to do it

`CustomName` participates in item equality:

```csharp
[Flags]
public enum ItemEntryCompareMask
{
    None = 0, Amount = 1,
    QualitySame = 2, QualityOrHigher = 4, QualityOrLower = 8,
    Metadata = 0x10,
    CustomName = 0x20,
    Metadata_CustomName = 0x30,
    QualitySame_Metadata_CustomName = 0x32,     // <-- the default nearly everywhere
    QualityOrHigher_Metadata_CustomName = 0x34
}
```

`QualitySame_Metadata_CustomName` is the default mask used throughout `GameInventory`,
`Inventory`, `CraftingHelper`, shop screens, and quest checks (`FetchJobPersistence`,
`CheckInventoryForBundle`).

`Chest` implements `IsAllowedToAddToInventory`, so **chests can be picked up into the
player's inventory.** A named chest would then be a *different item* from an unnamed chest
for stacking, crafting, selling, and any quest that asks the player to have N chests.

That's a real, silent, hard-to-diagnose bug. For creatures it's correct behavior — each
animal is meant to be unique. For chests it is not.

**Decision: do not write to `CustomName`.** Use a sidecar file keyed by chest GUID.

---

## 3. Where to hook "player opened a chest"

```csharp
public class PlayerStorageChestState : BasePlayerChestState
{
    private Chest chest;

    protected override void OnActivate()
    {
        base.OnActivate();
        chest = base.data.Get<Chest>(0);
        chest.Open();
        UIScreen<ChestScreen>.Instance.Show(chest);
        ...
    }

    protected override void OnDeactivate() { ... }
}
```

`PlayerStorageChestState.OnActivate` is the hook. A Harmony `Postfix` there gives access to
the `Chest` instance — and therefore `chest.GridObjectPersistence.Guid` — at the exact
moment the UI opens. `OnDeactivate` is the matching teardown.

Reached via `Chest.ResolveInteraction`:

```csharp
public override void ResolveInteraction(PlayerStateMachine playerStateMachine)
{
    playerStateMachine.GotoState<PlayerStorageChestState>(new object[1] { this });
}
```

---

## 4. `ChestScreen` has no title element

```csharp
public class ChestScreen : BaseUIScreen<ChestScreen>
{
    private Chest chest;
    public InventoryListWidget PlayerListWidget { get; private set; }
    public InventoryListWidget ChestListWidget { get; private set; }

    public void Show(Chest chest) { this.chest = chest; Show(); }
    protected override void OnShow() { ... }
}
```

Two inventory widgets, nothing else. **There is no header to repurpose** — we have to
instantiate our own text element into the screen's hierarchy. That's the bulk of the v1 UI
work, and the main unknown remaining.

Useful: the field is `private Chest chest`, reachable from a patch via Harmony's
`___chest` triple-underscore private-field accessor.

---

## 5. The nameplate system is NOT world-space

Worth documenting because it looks like a shortcut and isn't:

```csharp
public struct CustomNameplateData(string text) : INameplateData { public readonly string Text = text; }

public class NameplateScreen : BaseUIScreen<NameplateScreen>
{
    public void Show(RectTransform target, INameplateData nameplateData) { ... }
}
```

`Show` anchors to a **`RectTransform`** — it's a UI-space hover tooltip (used for inventory
slots, shop entries, museum entries), not floating world text.

`CustomNameplateData` does let us feed arbitrary text into the game's own tooltip styling,
which is genuinely useful. But **v2 floating labels above chests in the world will need our
own world-space canvas.** Budget for that; it is not free.

---

## 6. Available to us

Already loaded by the game, so no shipping dependencies:

- **Newtonsoft.Json** — sidecar serialization, no extra DLL
- **DOTween** — if we want the label to fade
- `LocalizationLibrary.Translate("kebab-case-key")` — for any UI strings

---

## Open questions — next session

1. **Save-file identity.** A sidecar keyed only by chest GUID would leak labels across
   different save files. Need to find how `GamePersistence` identifies the active save, and
   scope the sidecar to it. **This is the top unknown.**
2. **Chest deletion.** `Chest.Delete()` exists and nulls the persistence. Patch it to prune
   the label, or labels accumulate forever in the sidecar.
3. **Multiple rooms.** `Inventory` is created from `GamePersistence.Instance.CurrentRoom.Inventories`
   — per-room. Confirm GUIDs are globally unique and not per-room, or the sidecar needs a
   room key too.
4. **Text input.** Find how `DirectorNameNewCreatureState` collects typed input; reuse that
   rather than building a text field from scratch.
5. **`ShippingChestScreen` / `PlayerShippingChestState` / `PlayerTreasureChestState`** — the
   shipping and treasure chests are separate states. Decide whether they're in scope (v1:
   no, storage chests only).
