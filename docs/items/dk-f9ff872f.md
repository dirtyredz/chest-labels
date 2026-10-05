---
id: dk-f9ff872f
type: task
created: 2026-10-05
status: todo
since: 2026-10-05
area: core
priority: P2
rank: zn
parent:
fixes: []
blocked_by: []
relates: []
---
# Move core/UiDiagnostics.cs into ui/

From docs/BACKLOG.md (Placement follow-ups, 2026-09-01 structure review)

- **P2 — `core/UiDiagnostics.cs` belongs in `ui/`.** It takes `ChestScreen`, `TextMeshProUGUI`,
  `Canvas` and `RectTransform` and walks the live chest panel hierarchy; its only caller is
  `ui/ChestHeaderPresenter.cs`. `core/` is supposed to be game-type-free — `LabelStore.cs` genuinely
  is. Moving it would also make `core/` safe to glob, which matters because
  `tests/ChestLabels.Tests.csproj` compiles `LabelStore.cs` by explicit path (it targets net8.0 with
  no Unity refs, so a `core/*.cs` glob would currently pull in `UiDiagnostics.cs` and fail).
  The same "diagnostics landed in core/" mistake appears in ModNook and CoffinBreak; the workspace
  `core/` definition has been corrected so it no longer licenses it.
