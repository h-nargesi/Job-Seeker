# Chat 10 — Reports Phase 1: Data Foundations

**Status:** done 2026-09-27. **Depends on:** Chat 9 (independent track; first
reports/chat per DASHBOARD_CHARTS.md §6 roadmap step 1).

## Goal

Make `ModifiedOn` trustworthy and `RegTime`/`ModifiedOn` local-time, as
required by [`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §2 rules 1–2
before any chart or stats endpoint can consume them:

1. **ModifiedOn guard** — informational writes must not bump `ModifiedOn` on
   terminal rows (`Applied`/`Rejected`), so disposition time survives.
2. **Local-time timestamps** — new rows get `DateTime.Now`, not the SQLite
   `current_timestamp` UTC default.

User-approved deviation from the doc: the guard also covers
`Q_APPLY_VERDICT` (the informational AI-verdict path can hit terminal rows;
state promotion only happens from `AiPending`, so the guard is safe there).

No charts, endpoints, stats SQL, or index scripts in this phase.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §2 (rules), §6 (roadmap step
1). Legacy rows keep their UTC skew by design — no migration.

## Files

- `core-decision-dotnet/Database/Business/JobBusiness.Sql.cs` — new
  `ModifiedOnGuard` const (`CASE WHEN State IN ('Applied','Rejected') THEN
  ModifiedOn ELSE @now END`, built with `nameof`); applied to
  `Q_UPDATE_CONTENT`, `Q_REGISTER_ATTEMPT`, `Q_CHANGE_OPTIONS`,
  `Q_SAVE_RESUME_TEXT`, `Q_REMOVE_HTML`; both INSERTs now write explicit
  `RegTime`/`ModifiedOn` bound to `@now`.
- `core-decision-dotnet/Database/Business/JobBusiness.Ai.cs` — guard applied
  to `Q_APPLY_VERDICT`.
- `core-decision-dotnet/Database/Business/JobBusiness.cs` — guard applied to
  the dynamic `UpdateScrapedJob` UPDATE; `InsertFromSearch`/`InsertJob` pass
  `now = DateTime.Now`.
- `database/structure/job.sql` — column defaults changed to
  `datetime('now','localtime')`.
- `database/updates/20260927-02-job-localtime-defaults.sql` (new — added on
  user request after the plan deferred it) — rebuilds `Job` (create-new /
  copy / drop / rename) so existing installs get the same local-time
  defaults. Safe because nothing references `Job` via FK or index. All row
  values are copied byte-identical: legacy `ModifiedOn` values are already a
  UTC (initial default) + local (every `@now` update) mix, so a wholesale
  UTC→local shift would corrupt local-written rows — the skew stays by
  design.
- `core-decision-dotnet.Tests/JobLocaltimeDefaultsMigrationTests.cs` (new)
  — executes the update script against a legacy-schema in-memory DB
  (`current_timestamp` defaults): rows/values preserved, `unique
  (AgencyID, Code)` intact, DDL now `localtime`, fresh inserts without
  timestamps get local time.
- `core-decision-dotnet.Tests/JobModifiedOnGuardTests.cs` (new) — follows the
  `GoldenDatabase` harness / `ModifiedOn` string-compare /
  `Thread.Sleep(1100)` pattern from `PhaseAGoldenTests.cs`.

Unconditional `ModifiedOn = @now` stays in the state-change queries
(`Q_CHANGE_STATE`, `Q_MANUAL_STATE`, `Q_MARK_APPLIED`,
`Q_FETCH_UPDATE_REVAL`, `Q_RESURRECT`, `Q_REQUEUE`, `Q_PROMOTE`,
`UpdateEvaluation`) — a state change is a real disposition event.

## Tests

`JobModifiedOnGuardTests`:

- Each guarded write (`UpdateJobContent`, `RegisterAttempt`,
  `ChangeOptions`, `WriteLiveText`, `RemoveHtmlContent`, `UpdateScrapedJob`,
  informational `ApplyAiVerdict`) on `Applied` + `Rejected` rows →
  `ModifiedOn` unchanged (data columns verified written).
- All guarded writes on non-terminal rows (`Attention`/`Saved`, incl.
  informational verdict) → `ModifiedOn` still bumps.
- `ChangeStateManually` / `MarkApplied` still bump.
- `InsertFromSearch` / `InsertJob` store `RegTime`/`ModifiedOn` within 90 s
  of `DateTime.Now` (local time; passes trivially on UTC machines).

## Must not

Charts/endpoints/stats SQL/index scripts (later roadmap steps). Migration of
legacy timestamp *values* (UTC/local mix — unconvertible). Touching
test-harness DDLs that still default to `current_timestamp` (harmless once
INSERTs are explicit). Status header flips in DASHBOARD_CHARTS.md / TODO.md
(roadmap step 7 wrap-up).

## Validate

```
dotnet build Job-Seeker.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

(Repo solution file is `Job-Seeker.sln`.)

## Done

All guarded writes preserve terminal `ModifiedOn`; timestamps of new rows
are local time; full suite green, build clean.
