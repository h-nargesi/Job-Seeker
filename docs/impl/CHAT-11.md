# Chat 11 — Reports Phase 2: Stats Infrastructure

**Status:** done 2026-09-27. **Depends on:** Chat 10 (phase 1, committed as
`87c8ab3`). Implements [`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6
roadmap step 2.

## Goal

Ship the stats plumbing with **locked endpoint/DTO contracts but empty data**:
vendored Chart.js, `/report/stats` page skeleton, `/report/statsdaily` +
`/report/statsfull` JSON envelopes, dashboard chart mount + 15 s refresh
skeleton, and controller tests. No charts, no stats SQL, no index script —
those are roadmap steps 3+.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §1 (page split), §3 (D1–D3
semantics — frozen into DTOs), §5 (endpoints, vendoring, refresh, budgets),
§6 (roadmap step 2).

## Files

- `core-decision-dotnet/wwwroot/scripts/lib/chart.umd.js` (new) — vendored
  **Chart.js v4.4.9** UMD (latest 4.4.x), downloaded from jsdelivr, committed
  as-is with its license header (trailing `sourceMappingURL` comment kept;
  no `.map` file vendored). Same file will serve the `/monitor` deferred
  chart.
- `core-decision-dotnet/Analyze/Models/Stats/` (new) —
  `StatsDailyResponse` (+ `DailyStackedItem` D1 7-segment rows,
  `VelocityItem` D2, `StatsKpis` D3), `StatsFullResponse` (+ `StatsFilters`),
  `StatsPageViewModel` (filter prefill). S-chart sections deliberately absent
  — they get added by their own phases.
- `core-decision-dotnet/Controllers/Report.cs` — class marked `partial`
  (primary constructor + `[Route]` stay on the main declaration only).
- `core-decision-dotnet/Controllers/Report.Stats.cs` (new) — `Stats` (view,
  prefills filters from query), `StatsDaily` (envelope, `Days`=30, sections
  null), `StatsFull` (envelope + parsed filter echo). `ParseFilters` mirrors
  the `GetJobs` comma-split convention and additionally trims segments so
  empty/whitespace segments drop.
- `core-decision-dotnet/Views/stats.cshtml` (new) — Charts page skeleton:
  filter inputs (Enter/Apply navigates to `/report/stats?...`), placeholder
  sections for the §1 layout groups, phase note.
- `core-decision-dotnet/Views/dashboard-chart.cshtml` (new) — placeholder
  mount partial (`#dashboard-chart-body`), referenced from `index.cshtml`
  after the main row.
- `core-decision-dotnet/Views/index.cshtml` — Charts button next to AI
  Monitor; partial include; `chart.umd.js` + `dashboard-chart.js` script
  tags at the end of the body (after their DOM container; layout scripts
  already load after page content).
- `core-decision-dotnet/wwwroot/scripts/dashboard-chart.js` (new) — 15 s
  `setInterval` fetch of `/report/statsdaily` (own
  `DASHBOARD_CHART_REFRESH_MS` constant, `server-operations.js` pattern,
  element-guarded + reentrancy flag), verifies `window.Chart`, no-op on the
  data until phase 3 supplies datasets.
- `core-decision-dotnet.Tests/ReportStatsTests.cs` (new) — see Tests.

## Decisions recorded

- **Script tags land in phase 2** (deviation from a literal "Chart.js only on
  the stats page" reading of §1): loading `chart.umd.js` +
  `dashboard-chart.js` from the dashboard now makes the load path
  smoke-testable (`window.Chart` defined) before phase 3 renders D1–D3.
- **Index script deferred to phase 3** (deviation from §5's "when the stats
  endpoints land" wording): no `Q_STATS_*` query executes in phase 2, so
  there is nothing to index or `EXPLAIN` yet.
- Auth untouched: `dashboard` role path rules are empty → `/report/stats*`
  already covered when `Auth:ApiKeys:Dashboard` is configured.

## Tests

`ReportStatsTests` (CheckpointDatabase harness, direct controller calls):
`StatsDaily` serves the envelope with `Days`=30 and null D1/D2/D3 sections;
`StatsFull` echoes trimmed/empties-dropped filters and returns empty arrays
without filters; `Stats` returns `~/views/stats.cshtml` with the raw filter
strings on the model.

## Must not

`Q_STATS_*` SQL, `JobBusiness.Stats.cs`, chart rendering, D1–D3/S* datasets,
`database/updates/*-indexes.sql`, `docs/API.md` entries,
`DASHBOARD_CHARTS.md` status header, TODO.md checkbox (all later roadmap
steps). No auth/middleware changes.

## Validate

```
dotnet build Job-Seeker.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

Manual smoke (`dotnet run --project core-decision-dotnet`): `/` shows the
Charts button and `window.Chart` is defined; `/report/stats` prefills from
`agencies`/`countries`; `/report/statsdaily` and `/report/statsfull` return
200 JSON envelopes.

## Done

Build clean; new tests 4/4 green; full suite 439 passed with only the 3
pre-existing `LinkedInMarkupTests` fixture failures (missing
`examples/*.html` on this machine, unrelated to this phase).
