# Releasing Chest Labels

Repo-wide rules live at the root; this file only covers what is specific to this mod.

- Versioning and archive layout: [12-versioning-and-release.md](../../12-versioning-and-release.md)
- Visual integration: [10-visual-integration.md](../../10-visual-integration.md)
- Save safety: [11-mod-data-and-saves.md](../../11-mod-data-and-saves.md)

Short version on numbering: the version is for players, not a build counter. Bump it only
when publishing, one CHANGELOG entry per release.

## Build a release

```bash
powershell -File mods/ChestLabels/pack.ps1
```

Produces `dist/ChestLabels-<version>.zip`, reading the version from the csproj so the archive
can never disagree with the DLL. Runs the tests first and refuses to package if they fail.

## Pre-release checklist

Root checklist first: [12-versioning-and-release.md](../../12-versioning-and-release.md).
Then the items specific to this mod:

- [ ] Tested with **no label set** - the pencil must still appear so a chest can be named
- [ ] Tested renaming, Escape-cancel, and that chest hotkeys do not fire while typing
- [ ] Tested the hover at the edge of interaction range - the label and the game's arrow must
      appear and disappear together
- [ ] Confirmed the game save is untouched (`CustomName` count still 0)
- [ ] Screenshots show the current build, and use plausible chest names

## Verifying save safety

The core promise of this mod. Worth re-checking whenever storage code changes. Saves are
gzipped despite the `.json` extension:

```powershell
$f = "$env:USERPROFILE\AppData\LocalLow\Little Chicken Game Company\Moonlight Peaks\<steam-id>\Saves\<save-guid>\GameData.json"
$bytes = [System.IO.File]::ReadAllBytes($f)
$in = New-Object System.IO.MemoryStream(,$bytes)
$gz = New-Object System.IO.Compression.GZipStream($in,[System.IO.Compression.CompressionMode]::Decompress)
$out = [System.IO.File]::Create("check.json"); $gz.CopyTo($out); $gz.Dispose(); $out.Dispose(); $in.Dispose()
$txt = [System.IO.File]::ReadAllText("check.json")
"CustomName occurrences: " + ([regex]::Matches($txt,'"CustomName"')).Count   # must be 0
```

Also search for one of your label strings. It must not appear.

## Licence

**MIT** - see [LICENSE](../../LICENSE) at the repo root. Permissive: anyone may use, modify
and redistribute, provided the copyright notice is kept.

Set the Nexus permissions to agree with it, or the page and the licence contradict each other:

| Nexus permission | Set to |
|---|---|
| Upload to other sites | Allowed |
| Convert to other games | Allowed |
| Modify and release | Allowed |
| Use assets in own files | Allowed |
| Include in mod packs / collections | Allowed |

Credit is customary rather than required under MIT. Asking for it in the description is fine;
do not set a permission that MIT already grants.

## Repository

`origin` -> <https://github.com/dirtyredz/chest-labels.git>

Public, but deliberately **not linked from the Nexus page** for now. Those are separate
decisions: the code being reachable is not the same as advertising it to players.

Repo metadata to set on GitHub (About):

- **Description:** Chest Labels - a Moonlight Peaks mod for naming your chests. Save-safe, BepInEx 5.
- **Topics:** `moonlight-peaks` `bepinex` `harmony` `unity` `modding` `csharp`

## Editing note

Do not round-trip these files through `Get-Content -Raw | Set-Content` in PowerShell. It
re-encodes non-ASCII characters and has corrupted em-dashes here twice. Files are kept in
plain ASCII partly for that reason; edit them with a tool that preserves encoding.
