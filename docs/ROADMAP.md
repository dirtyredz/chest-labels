# ROADMAP — Chest Labels

Status of the trajectory. Detailed tasks live in [BACKLOG.md](BACKLOG.md).

## Shipped
- **0.6.0 (2026-08-03)** — first public release: in-window rename, chest-window title, world hover,
  per-save GUID-keyed sidecar, hotkey suppression while renaming, corrupt-file quarantine.
- **0.7.0 (2026-08-03)** — visual integration: game nameplate for hover, shared interaction-target
  detection, arrow suppression, Gelica-Black title, explicit font resolution everywhere.
- **0.7.1 (2026-08-03)** — rename field matches the title's font/size/colour.
- **1.0.0 (2026-08-10)** — screen-reader support via the `CustomName` overlay.
- **1.0.1 (2026-08-20)** — foreign-patch guard for the shared nameplate (game 1.2.3 compatibility).

## In progress
- 🛠 **Internal structure health** — `ChestPatches` God-file split done (2026-08-22); living docs +
  structure-review gate adopted.

## Planned / candidate
- 📋 **Split `HoverLabel`** into detection / own-plate view / game-nameplate view (P1 structural debt).
- 📋 **Automated coverage beyond `LabelStore`** where feasible without a running game (P2).
- 📋 Feature direction is owner-driven and largely reactive to game updates; no committed feature
  milestones beyond keeping current with Moonlight Peaks releases. _(Confirm with owner.)_

_Living doc — refresh with /project-docs when it drifts._
