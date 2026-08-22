# CLAUDE.md — Chest Labels

How to work in this mod. Orientation lives in the doc set — read those, don't duplicate them here.
The workspace root [../../CLAUDE.md](../../CLAUDE.md) covers cross-mod conventions and the two-layer
git layout; this file is the ChestLabels-specific quick-start.

- **[STRUCTURE.md](STRUCTURE.md)** — code-shape map (components, deps, where things live, debt).
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — how the system works (flows, data, interfaces).
- **[docs/DECISIONS.md](docs/DECISIONS.md) · [FEATURES.md](docs/FEATURES.md) ·
  [ROADMAP.md](docs/ROADMAP.md) · [BACKLOG.md](docs/BACKLOG.md) · [GOTCHAS.md](docs/GOTCHAS.md)**

## Build / test / deploy

```bash
dotnet build src/ChestLabels.csproj -p:SkipDeploy=true   # compile only
dotnet run --project tests/ChestLabels.Tests.csproj       # LabelStore tests (the only coverage)
dotnet build src/ChestLabels.csproj -c Release            # build + DEPLOY the DLL into the game
```

Only `LabelStore` is unit-tested. **All UI / hover / geometry code must be smoke-tested in-game**
(open a labeled chest; hover a chest) after any change — see [docs/GOTCHAS.md](docs/GOTCHAS.md).

## Conventions (mod-specific)

- Plugin `.cs` flat in `src/`; docs + `pack.ps1` at mod root.
- Version in csproj `<Version>` only, only when publishing; never hardcode in `Plugin.cs`.
- Commit identity `dirtyredz <dirtyredz@live.com>`.
- **Do NOT edit `pack.ps1` or `Directory.Build.props`** — workspace-synced canonicals.
- Pack/publish: `pack.ps1` → `dist/ChestLabels-<version>.zip`; publish via the workspace
  **nexus-publish** skill. Full chain in the workspace `docs/ARCHITECTURE.md`.

## Structure-review gate

This repo is gated (pre-push hook). Edit/debug freely; the review fires once at **push** on the
accumulated change, not per edit or commit. Commit at logical boundaries; Claude runs the review and
pushes (asking first) when work is ready. `/gate status` shows what's pending.

_See the workspace root CLAUDE.md for the full gate + doc-set workflow._
