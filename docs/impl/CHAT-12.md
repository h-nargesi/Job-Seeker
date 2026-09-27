# Chat 12 — Reports Phase 3: Dashboard Charts (D1–D3)

**Status:** done 2026-09-27. **Depends on:** Chat 11 (phase 2). Implements
[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6 roadmap step 3.

## Goal

Fill `/report/statsdaily` with real data and render the dashboard charts:
**D3** KPI tiles (avg disposition days; Attention backlog count + avg age),
**D1** daily stacked bar by State (7 segments), **D2** velocity lines
(Applied/day, Rejected/day), auto-refreshing every 15 s with in-place dataset
updates. Also ships the stats index script deferred from phase 2.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §2 rules 3–4 (age/velocity
semantics), §3 (D1–D3), §5 (refresh, budgets, indexing, SQL rules), §6 step 3.

## Files

- `core-decision-dotnet/Database/Business/JobBusiness.Sql.cs` — added
  `Q_STATS_DAILY_STACKED` (7-segment counts grouped by
  `SUBSTR(RegTime,1,10)`, window `RegTime >= @from`), `Q_STATS_VELOCITY`
  (Applied/Rejected grouped by `SUBSTR(ModifiedOn,1,10)`), `Q_STATS_KPIS`
  (avg disposition via `JulianDay(ModifiedOn) - JulianDay(COALESCE(PublishedAt,
  RegTime))` over Applied/Rejected; Attention backlog count; Attention avg
  age against `@now`). Enum names via `nameof(JobState.X)`; `@now`/`@from`
  always C# parameters, never SQLite `'now'`.
- `core-decision-dotnet/Database/Business/JobBusiness.Stats.cs` (new) —
  `StatsDailyStacked(days)`, `StatsVelocity(days)` (both zero-fill the whole
  window from `DateTime.Now.Date.AddDays(-(days-1))` so the chart x-axis is
  continuous), `StatsKpis()` (rounds the two day-averages to 1 decimal).
- `core-decision-dotnet/Controllers/Report.Stats.cs` — `StatsDaily` now
  populates the three envelope sections (contract unchanged).
- `core-decision-dotnet/Views/dashboard-chart.cshtml` — real mount: 3 KPI
  tiles (`#kpi-avg-disposition`, `#kpi-attention-backlog`,
  `#kpi-attention-age`) + two 260px chart cards (`#daily-stacked-chart`,
  `#velocity-chart`).
- `core-decision-dotnet/wwwroot/scripts/dashboard-chart.js` — renders D3 via
  `textContent`, D1 stacked bar + D2 line chart; datasets updated in place on
  each 15 s tick (charts never rebuilt on refresh); first load fires
  immediately so charts are not empty for a tick; dark/light theme followed
  via a `MutationObserver` on `data-bs-theme` (charts rebuilt with new
  colors from cached data). Bootstrap-palette segment colors, `precision: 0`
  y ticks, `MM-DD` labels.
- `core-decision-dotnet/Views/stats.cshtml` — placeholder corrected: D2/D3
  live on the dashboard (§1 sketch was stale; §3/§4 and the D/S naming put
  them there).
- `database/updates/20260927-03-stats-indexes.sql` (new) — §5 index script:
  `Job(State)`, `Job(RegTime)`, `Job(ModifiedOn)`, `Job(AiVerdict)` for the
  stats/monitor hot paths, `AiRun(StartedUtc DESC, RunID DESC)` for
  `AiRunBusiness.Recent`.
- `core-decision-dotnet.Tests/JobStatsTests.cs` (new) + `ReportStatsTests.cs`
  — see Tests.
- `docs/DASHBOARD_CHARTS.md` — §1 sketch fixed (D1–D3 all on dashboard, stats
  page S-charts only); §6 roadmap steps 1–3 marked done. Status header flip
  stays deferred to step 7.

## Decisions recorded

- **Zero-fill in C#, not SQL**: the window days come from
  `StatsWindowDays` so every response carries exactly `Days` rows and the
  frontend never fills gaps.
- **`from` is local midnight** (`DateTime.Now.Date.AddDays(-(days-1))`);
  legacy UTC rows keep their accepted skew (§2 rule 2, no migration).
- **First fetch on script load** (interval alone was the phase-2 skeleton):
  without it the charts stay empty up to 15 s after page load.
- **Theme rebuild, not recolor**: Chart.js bakes default colors at
  construction, so on theme change both charts are destroyed and rebuilt
  from the cached last payload — simpler and exact.
- Indexes ride this phase per the Chat 11 deferral note: this is the first
  phase where `Q_STATS_*` actually execute.

## Tests

`JobStatsTests` (GoldenDatabase): D1 buckets all 7 segments by RegTime day
and keeps older days in-window; D1 zero-fills days without jobs; D2 buckets
Applied/Rejected by ModifiedOn day (an old-RegTime Applied lands on its
ModifiedOn day; Attention excluded); KPIs average disposition over
`COALESCE(PublishedAt, RegTime)` (exact 3d/1d → avg 2.0), Attention backlog
2 with avg age in (7.5, 8.5); empty DB → null averages, backlog 0.
`ReportStatsTests.StatsDaily_returns_zero_filled_sections_for_last_30_days`
replaces the phase-2 null-sections test (envelope contract now filled; 30
zero rows ending today; KPIs null/0).

## Must not

S-chart datasets, `statsfull` data, `docs/API.md` entries, status-header
flip, TODO.md checkbox (roadmap steps 4–7). No auth/middleware changes.

## Validate

```
dotnet build core-decision.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

Manual smoke (`dotnet run --project core-decision-dotnet`): `/` renders the
three KPI tiles and both charts with data on first load; `AGEN`/network tab
shows `/report/statsdaily` re-fetched every 15 s with charts updating in
place; theme toggle recolors ticks/legend/grid; `/report/stats` placeholders
no longer mention D2/D3.
