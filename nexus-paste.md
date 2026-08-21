> ⚠️ **Superseded — do not paste from this file.**
> The live pages were restyled on 2026-08-04 and this BBCode is the *pre-style* version.
> The live page is now the source of truth; pull its BBCode from the edit form's description
> field. Structure: [14-description-review.md](../../14-description-review.md). Look:
> [15-page-style.md](../../15-page-style.md). Mechanics: [13-nexus-page-standard.md](../../13-nexus-page-standard.md).

# Chest Labels — Nexus page source

**Nexus page:** [mod 119](https://www.nexusmods.com/moonlightpeaks/mods/119)

The description field is **SCEditor with a BBCode source**, so the block below is the literal
value that gets set — not something to be retyped through a toolbar. Structure per
[14-description-review.md](../../14-description-review.md).

Description prose and Main features wording are **yours, unchanged**. Only the order, the
list markup and the Mod Nook / Mod Menu changes are mine.

## Other fields

| Field | Change |
|---|---|
| Name | `Chest Labels` — no change |
| Category | User Interface — no change |
| Tags | User Interface, Quality of Life — no change |
| Short description | replace, see below |

**Short description** (replaces *"Chest Labels — a Moonlight Peaks mod for naming your chests.
Save-safe, BepInEx 5."*):

```
Name your chests and see which is which — a title in the chest window and a label when you mouse over it.
```

## Description source

```bbcode
[size=4][b]Description[/b][/size]
[color=#D4D4D8]Six chests on your farm and no idea which one holds the ore.

Chest Labels lets you name them. The name appears as a title inside the chest window, and floats above the chest when you mouse over it — so you can tell them apart without opening a single one.

Renaming happens in game. Open a chest, click the pencil beside the title, type the name. Enter saves, Escape cancels. No config files, no restarts.

Your names are stored in the mod's own file and never written into your save game. Uninstall Chest Labels and your save is exactly what the game wrote.[/color]

[size=4][b]Main features[/b][/size]
[list]
[*]Name any storage chest, from inside the chest window
[*]The name shows as a title in the chest window's own header, under the game's decoration
[*]Mouse over a chest in the world to see its name floating above it
[*]Rename in game with the pencil button — Enter to save, Escape to cancel
[*]Names survive picking a chest up and placing it somewhere else
[*]Deleting a chest removes its name, so nothing builds up over time
[*]Names are stored per save file, so separate playthroughs never mix
[*]The hover label stays out of the way during cutscenes, menus and dialogue
[*]Every part is adjustable, and the title and hover label can be switched off independently
[/list]

[size=4][b]Requirements[/b][/size]
[list]
[*][b]BepInEx 5 (win_x64)[/b], version 5.4.23.5 or newer — the only thing this mod needs
[/list]
[color=#D4D4D8]PC/Steam only. The Switch and mobile builds cannot load BepInEx.[/color]

[size=4][b]Installation[/b][/size]
[b]With Vortex[/b]
[color=#D4D4D8]Open the Files tab, click the Vortex button, and enable the mod. Done.[/color]

[b]Manually[/b]
[list=1]
[*]Install [b]BepInEx 5 (win_x64)[/b] into your Moonlight Peaks folder, if you do not have it already. The BepInEx folder sits beside Moonlight Peaks.exe.
[*]Launch the game once, then quit. This creates the BepInEx/plugins folder.
[*]Download the archive from the Files tab and extract it over your Moonlight Peaks folder, so the file ends up at BepInEx/plugins/ChestLabels/ChestLabels.dll
[*]Launch the game.
[/list]
[color=#D4D4D8]To uninstall, delete the BepInEx/plugins/ChestLabels folder. Your save is untouched, because nothing was ever written to it.[/color]

[size=4][b]Configuration[/b][/size]
[color=#D4D4D8]Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.chestlabels.cfg on first launch. The defaults are meant to be left alone.

Install [url=https://www.nexusmods.com/moonlightpeaks/mods/127][b]Mod Nook[/b][/url] and you can change them in game instead. Chest Labels shows up in it on its own, so you can set the nameplate tint with a colour picker and switch the title or the hover label off without opening a file. Nothing here needs it — it just makes this mod easier to live with.[/color]

[size=4][b]Compatibility[/b][/size]
[color=#D4D4D8]Works alongside other BepInEx mods. Tested with Extra Tooltip, Detailed Minimap, Far Sight, Save Anywhere and FasterGrowth.[/color]

[size=4][b]Shout outs[/b][/size]
[list]
[*][b]Little Chicken Game Company[/b] for making a game worth spending this much time inside.
[*]The [b]BepInEx[/b] and [b]HarmonyX[/b] teams, without whom none of this scene exists.
[*]Whoever wrote the modding guides on the official wiki — they turn a cold start into an afternoon.
[*][b]My Mate[/b], for being my inspiration.
[/list]
```
