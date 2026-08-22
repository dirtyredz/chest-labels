# CLAUDE.md — working in the Chest Labels mod

A BepInEx 5 / HarmonyX plugin for *Moonlight Peaks* (Unity Mono, netstandard2.1). This is a
**standalone git repo** nested in the Moonlight Peaks workspace — when you work here, THIS repo is
the active project (its own gate, its own baseline), not the workspace root. Orientation lives in
the doc set; read those rather than duplicating here.

- **[README.md](README.md)** — design narrative, decisions, Nexus status.
- **[STRUCTURE.md](STRUCTURE.md)** — code-shape map + structural debt.
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** · **[DECISIONS](docs/DECISIONS.md)** ·
  **[FEATURES](docs/FEATURES.md)** · **[ROADMAP](docs/ROADMAP.md)** ·
  **[BACKLOG](docs/BACKLOG.md)** · **[GOTCHAS](docs/GOTCHAS.md)**

## Build / test / pack

- Compile-check without touching the game install: `dotnet build src/ChestLabels.csproj -p:SkipDeploy=true`
  (a plain build deploys the DLL to the game's plugin folder).
- Run the unit tests: `dotnet run --project tests/ChestLabels.Tests.csproj` (no framework, no game).
- Package a release: `pwsh ./pack.ps1` → `dist/ChestLabels-<version>.zip` in Nexus layout.
- **Do not launch the game** as part of routine work.

## Conventions

- Commit identity: `dirtyredz <dirtyredz@live.com>`.
- Plugin `.cs` flat in `src/` (no `src/<ModName>/`). Version single-sourced from
  `src/ChestLabels.csproj` `<Version>` — never hardcode it in `Plugin.cs`. Bump only when publishing.
- `Directory.Build.props` and `pack.ps1` are **workspace-synced canonicals** — edit the originals
  under `../../tools/`, never the copies here.
- Every drawn element must look native (see the README design principle + [docs/GOTCHAS.md](docs/GOTCHAS.md)).
- Keep `LabelStore` free of Unity/BepInEx types so the tests run without the game.

## Structure-review gate

Installed 2026-08-22 (pre-push hook in the common git dir). Edit/debug freely; the review fires once
at **push** on the accumulated change. Commit at logical boundaries; Claude runs the review and
pushes (asking first) when work is ready. `/gate status` shows what's pending.

_See the workspace root [../../CLAUDE.md](../../CLAUDE.md) for the multi-repo rules._
