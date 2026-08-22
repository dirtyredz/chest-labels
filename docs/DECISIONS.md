# DECISIONS — Chest Labels

Significant decisions worth not re-litigating, newest first. Rationale drawn from the README,
`research/`, and git history; where a date is approximate it is marked.

## 2026-08-22 — Split the review doc set out; keep the God-file split as backlog, not a review fix

**What:** The full structural review fixed only zero-risk items (extract `ChestIdentity`, delete dead
code, correct the README) and backlogged the large `ChestPatches`/`HoverLabel` extractions.
**Why:** The big extractions move UI-geometry code that can only be verified by launching the game,
which this pass could not do; shipping them unverified is riskier than the God-file smell they cure.
**Rejected:** Doing the full split now — too much untested UI churn for one review pass.

## 2026-08-03 — Ship 1.0.0; single-source the version from the csproj

**What:** First public release as Nexus mod 119. `[BepInPlugin]` version comes from a generated
`ModBuildInfo.Version` fed by the csproj `<Version>`.
**Why:** A hardcoded version string in `Plugin.cs` drifts from the packaged zip. One source of truth.
**Rejected:** Hardcoding the version; duplicating it in `pack.ps1`.

## 2026-08-03 — Screen-reader support via a read-time `CustomName` overlay (1.0.0)

**What:** Patch the `GridObjectPersistence.CustomName` getter to return the label when the field is
empty, so MoonlightAccess announces named chests.
**Why:** MoonlightAccess already reads `CustomName`; filling it at read-time needs no new UI and no
save write.
**Rejected:** Writing `CustomName` into the save (breaks item stacking/identity — see below) — the
getter overlay gets the accessibility win without the corruption risk.

## ~2026-08 — Prefer the game's own interaction banner + nameplate over a custom plate

**What:** The world label rides the game's interaction target (via reflection) and renders through
`NameplateScreen`; the self-drawn plate is a fallback.
**Why:** Matching the game's trigger, font, colour, and animation exactly means one source of truth —
no second detection to drift out of sync with the interaction arrow.
**Rejected:** An always-custom overlay with its own raycast detection (produced arrow/label flicker
near interaction edges).

## ~2026-08 — Store labels in a per-save JSON sidecar, never in the game save

**What:** `BepInEx/config/ChestLabels/<save-guid>.json`, keyed by chest GUID.
**Why:** `ItemEntry.CustomName` is part of the default item-equality mask, so a chest picked up into
inventory would stop stacking/selling/counting correctly — silent, save-corrupting-adjacent. A
sidecar keeps the mod provably save-safe, which this community values.
**Rejected:** Reusing the game's `CustomName` field for persistence (the equality-mask trap);
a single global file (save isolation would be lost).

## ~2026-08 — Build the UI, don't borrow it

**What:** The in-panel title, hover plate, and pencil icon are constructed at runtime; fonts/colours
are resolved from the game's own assets.
**Why:** `ChestScreen` has no title element to repurpose, and `NameplateScreen` is UI-space, not
world-space. There is nothing to borrow, so it is real UI work — done to look native.
**Rejected:** Repurposing an existing screen element (none fits).

_Living doc — refresh with /project-docs when it drifts._
