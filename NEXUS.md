# Nexus Mod Page — Chest Labels

Draft copy for the Nexus listing. Nexus descriptions use **BBCode**, so a converted version
follows the readable draft below.

---

## Fields

| Field | Value |
|---|---|
| **Name** | Chest Labels |
| **Summary** (short, shows in listings) | Name your chests and see which is which — a title in the chest window and a label when you mouse over it. |
| **Category** | User Interface |
| **Version** | 0.6.0 |
| **Requirements** | BepInEx 5 (win_x64) — required |
| | Mod Menu — optional, for in-game settings |
| **Tags** | quality of life, user interface, storage, inventory, save-safe |
| **Licence** | MIT |

### Source link — deliberately omitted for now

The repository is public at <https://github.com/dirtyredz/chest-labels>, but the Nexus page
should **not link to it** for the initial release. The code being reachable and the code being
advertised to players are separate decisions.

Nothing about this conflicts with MIT, which governs what happens to the code once
distributed and says nothing about promoting it.

When there is a reason to invite contributions: Nexus has **no dedicated source-code field**
(checked against a live mod page — a page is Description / Requirements / Permissions and
credits / Changelogs, plus Logs and Stats tabs). Authors put repo links either in the
description body or beside the licence statement in Permissions and credits. Suggested line:

> Source is on GitHub under the MIT licence — issues and pull requests welcome.

---

## Full description — paste into Nexus

The upload form is a **rich-text editor** with a fixed five-section template, so paste each
block as plain text and use the toolbar for bold and bullets. Do not paste BBCode into it;
that only applies if you switch to source view.

---

### Description

Six chests on your farm and no idea which one holds the ore.

Chest Labels lets you name them. The name appears as a title inside the chest window, and
floats above the chest when you mouse over it — so you can tell them apart without opening a
single one.

Renaming happens in game. Open a chest, click the pencil beside the title, type the name.
Enter saves, Escape cancels. No config files, no restarts.

Your names are stored in the mod's own file and never written into your save game. Uninstall
Chest Labels and your save is exactly what the game wrote.

*(That last point is worth a line of its own — this community reads for it.)*

---

### Installation instructions

1. Install BepInEx 5 (win_x64) into your Moonlight Peaks folder, if you do not have it
   already. The BepInEx folder should sit beside Moonlight Peaks.exe.
2. Extract this mod into the same folder. It will land in BepInEx/plugins/ChestLabels.
3. Start the game.

To uninstall, delete the BepInEx/plugins/ChestLabels folder. Your save is untouched, because
nothing was ever written to it.

---

### Main features

- Name any storage chest, from inside the chest window
- The name shows as a title in the chest window's own header, under the game's decoration
- Mouse over a chest in the world to see its name floating above it
- Rename in game with the pencil button — Enter to save, Escape to cancel
- Names survive picking a chest up and placing it somewhere else
- Deleting a chest removes its name, so nothing builds up over time
- Names are stored per save file, so separate playthroughs never mix
- The hover label stays out of the way during cutscenes, menus and dialogue
- Every part is adjustable, and the title and hover label can be switched off independently

---

### Requirements

**Required**

- BepInEx 5 (win_x64)

**Optional**

- Mod Menu — adds a Mods page to the pause menu so you can change this mod's settings in
  game. Not needed; without it the settings live in a plain config file, and the defaults are
  meant to be left alone.

Works alongside other BepInEx mods. Tested with Extra Tooltip, Detailed Minimap, Far Sight,
Save Anywhere and FasterGrowth.

---

### Shout outs

- **Little Chicken Game Company** for making a game worth spending this much time inside.
- The **BepInEx** and **HarmonyX** teams, without whom none of this scene exists.
- Whoever wrote the modding guides on the official wiki — they turn a cold start into an
  afternoon.
- **Elsiabeth** for Mod Menu, which is why this mod's settings are configurable in game
  without it having to build a settings screen of its own.

---

## Changelog entries for the Nexus page

Player-facing. The repo `CHANGELOG.md` is written for us and names Harmony patches and
compare masks; that belongs in the repo, not on a mod page. Describe the **symptom** a player
would have noticed, not the cause, and leave out anything tried and reverted.

### 0.7.1

```
Fixed
- The chest name changed size and weight while you were editing it. The rename
  box now matches the title exactly.
```

### 0.7.0

```
New
- The hover label now uses the game's own speech bubble, so it looks like part of
  the game rather than an add-on. Its colour can be changed in the settings.
- The game's interaction arrow now steps aside while a named chest's label is
  showing, instead of the two overlapping.
- The chest window title is larger and bolder, in the game's own gold, with more
  space above and below it.

Fixed
- The hover label was using the wrong font. Every piece of text in the mod now
  uses the game's font, including the rename box.
- The label and the interaction arrow now appear and disappear together. There
  used to be a gap where the arrow showed but the label had not caught up.

New settings
- NameplateTint - colour of the speech bubble behind a chest name
- HideArrowWhenNamed - turn the arrow hiding on or off
```

### 0.6.0

```
First release.

- Name any storage chest from inside the chest window using the pencil button.
- The name appears as a title in the chest window and above the chest in the world.
- Names are stored in the mod's own file, never in your save game.
```

## BBCode version (only if using source view)

The upload form's editor is rich text — pasting this into it will show the tags literally.
Kept in case a future edit is made through a source/BBCode view, or for mirroring elsewhere.

```bbcode
[size=5][b]Chest Labels[/b][/size]

Six chests on your farm and no idea which holds the ore.

Chest Labels lets you name them. The name appears as a title in the chest window, and floats above the chest when you mouse over it, so you can tell them apart without opening a single one.

[b]Renaming is done in game.[/b] Open a chest, click the pencil beside the title, type. Enter saves, Escape cancels. No config files, no restarts.

[size=4][b]Save-safe, genuinely[/b][/size]

Your names are stored in the mod's own file, never in your save game. Uninstall Chest Labels and your save is byte-for-byte what the game wrote.

This was a deliberate design decision. The game does have a built-in custom-name field, and using it would have been less work — but that field is part of how the game decides whether two items are the same. A named chest would quietly stop stacking with unnamed ones, and would not count toward anything asking you to have a chest. Not worth it for a label.

[size=4][b]Details[/b][/size]

[list]
[*]Names survive picking a chest up and putting it somewhere else
[*]Deleting a chest removes its name; nothing accumulates
[*]Names are per save file, so different playthroughs stay separate
[*]The title sits inside the chest window's own header, under the game's decoration
[*]The hover label stays out of the way during cutscenes, menus and dialogue
[/list]

[size=4][b]Settings[/b][/size]

Everything is adjustable — text size, spacing, hover height and opacity, and both features can be switched off independently. Install [b]Mod Menu[/b] to change them from the pause menu, or edit the config file directly. Neither is required; the defaults are meant to be left alone.

[size=4][b]Installation[/b][/size]

[list=1]
[*]Install [b]BepInEx 5 (win_x64)[/b] into your Moonlight Peaks folder
[*]Extract this mod into the same folder
[*]Play
[/list]

To uninstall, delete [i]BepInEx/plugins/ChestLabels/[/i]. Your save is untouched.

[size=4][b]Compatibility[/b][/size]

Works alongside other BepInEx mods. Tested with Extra Tooltip, Detailed Minimap, Far Sight, Save Anywhere and FasterGrowth.
```

---

## Screenshots

Files live in `screenshots/`. Ordered by what sells the mod fastest — the first is the
gallery order. The thumbnail is set separately in the upload form - see below.

| # | Shot | File | Status |
|---|---|---|---|
| - | Thumbnail, 1672x941 (16:9) | `thumbnail.png` | ✅ made |
| - | Title banner, 2358x667 (3.5:1) | `banner.png` | ✅ made |
| 1 | Hover label on a chest in the world | `01-hover-world.png` | ✅ captured |
| 2 | Chest window with the title in its header | `02-chest-window.png` | ✅ captured |
| 3 | Mid-rename, text field open | `03-renaming.png` | ✅ captured |
| 4 | Two chests, one hovered and named | `04-two-chests.png` | ✅ captured |
| 5 | Mod Menu settings panel *(optional)* | `05-settings.png` | ⬜ optional |

All four gameplay shots retaken for 0.7.0; anything older shows the previous look and should
not be reused.

### Thumbnail and image order

**The upload form has a thumbnail setting** - you pick which image represents the mod, so it
is independent of gallery order. `thumbnail.png` is made for that slot at 16:9, the ratio a
listing tile expects.

That frees the gallery to be ordered for someone who has already clicked through, where
showing what the mod does matters more than branding:

1. `01-hover-world.png` - what the mod actually does
2. `02-chest-window.png`
3. `03-renaming.png` - renaming happens in game, which nothing else conveys
4. `04-two-chests.png`

`banner.png` at 3.5:1 suits the top of the description rather than the gallery; it would
letterbox in a tile.

### Worth knowing before using AI-assisted art

At least one mod in this scene advertises **"NO AI."** in its description as a selling point,
which implies the opposite draws comment here. Not a reason to avoid it - just a signal
specific to this community that is better known in advance than discovered in the comments.

All required shots are in. Shot 5 is genuinely optional — it reassures people the mod is
configurable, but nobody installs a mod because of a settings screenshot.

**Set the thumbnail explicitly** in the upload form rather than relying on gallery order -
there is a setting for it. Use `thumbnail.png`, which is made at 16:9 for that slot.

**Note on shot 4:** the original idea was two chests showing their names at once, which is
not achievable — the hover label only names the chest under the cursor, by design, so the
world is not cluttered with permanent text. The captured shot is the closest version: two
chests, one identified. It reads fine, and the pair naming ("Chest Labels" / "Are Awesome")
is a nice touch.

If a shot showing several names simultaneously ever matters for the listing, that would need
an always-on label mode, which is a feature decision rather than a screenshot problem.

Night shots read better against the game's lighting, and look unmistakably like
Moonlight Peaks.

## Notes before publishing

- Naming the demo chest **"Chest Labels"** reads well in the listing — the screenshot
  states what the mod does without a caption.
- State plainly in the description that it is **save-safe** — this community reads for that.
- List BepInEx as a **required** requirement and Mod Menu as **optional**.
- Decide a licence / permissions stance before upload (see RELEASING.md).
