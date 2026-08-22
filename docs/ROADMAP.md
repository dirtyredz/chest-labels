# ROADMAP — Chest Labels

One new system per version; each version is useful on its own.

## v1 — Label + rename + persistence ✅ shipped (1.0.0, 2026-08-03; 1.0.1 current)
Label in the open chest window, in-place rename, per-save JSON sidecar. Later folded in the world
hover label, interaction-banner name, and screen-reader support (all shipped).

## v2 — World-label polish 🔜 planned
Distance fade and richer world-space styling for the floating label. Builds on the existing
`HoverLabel` canvas. Best tackled **after** the P1 `HoverLabel` split (see
[BACKLOG.md](BACKLOG.md)) so new styling lands in `HoverLabelPlateView`, not the God-MonoBehaviour.

## v3 — Search / item locator 🔜 planned
Search across labels, growing into a cross-chest item locator ("where did I put the ore?"). New
system; largest scope.

## Structural track (parallel, not a product version)
Before major new chest-screen or hover work, land the P1 extractions in [BACKLOG.md](BACKLOG.md) so
the two God-files (`ChestPatches`, `HoverLabel`) don't keep accreting.

_Living doc — refresh with /project-docs when it drifts._
