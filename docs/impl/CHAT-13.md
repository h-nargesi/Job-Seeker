# Chat 13 — Reports Phase 4: Stats Page (S7, S1, S8)

**Status:** done 2026-09-27. **Depends on:** Chat 12 (phase 3). Implements
[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6 roadmap step 4.

## Goal

Fill `/report/stats` (`statsfull` payload) with the pipeline charts: **S7**
funnel Saved→Analyzed→Attention→Applied (overall + per agency), **S1** agency
yield (Analyzed/Accepted rates with counts), **S8** pipeline health daily
(AiPending backlog + AIError per day, 30-day zero-filled window). Manual
reload only, per §5.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §4 specs (S1, S7, S8),
§5 (endpoints, filters, budgets, SQL rules).

## Files

- `core-decision-dotnet/Analyze/Models/Stats/StatsFullResponse.cs` — envelope
  gained `AgencyYield`, `Funnel`, `PipelineHealth` sections; new records
  `StatsAgencyYieldItem`, `StatsFunnel`/`StatsFunnelStage`/`StatsFunnelAgency`,
  `PipelineHealthItem`.
- `core-decision-dotnet/Database/Business/JobBusiness.Sql.cs` — added
  `Q_STATS_AGENCY_YIELD` (per-agency JobCount/Analyzed/Accepted/Applied,
  LEFT JOIN so zero-job agencies appear; `@where@` = agency-title filter) and
  `Q_STATS_PIPELINE_HEALTH` (AiPending/AIError grouped by
  `SUBSTR(ModifiedOn,1,10)`, `@and@` = agency-title + country filters).
  Enum names via `nameof(JobState.X)`; `@from` is a C# parameter.
- `core-decision-dotnet/Database/Business/JobBusiness.Stats.cs` —
  `StatsAgencyYield(agencyTitles)` (rates computed in C# with the same
  truncation as `JobRateReport`), `StatsFunnel(yield)` (overall = sums,
  per-agency = rows with JobCount > 0; derived from the yield list so
  `StatsFull` runs the SQL once), `StatsPipelineHealth(days, agencies,
  countries)` (zero-fills the window like the daily charts). `StatsWindow`
  now returns `DynamicParameters` so filters can share it.
- `core-decision-dotnet/Controllers/Report.Stats.cs` — `StatsFull` fills the
  three sections (contract otherwise unchanged; 30-day window reuses
  `StatsDailyDays`).
- `core-decision-dotnet/Views/stats.cshtml` — pipeline section mounts: funnel
  card with agency `<select>` (`#funnel-chart`), health card
  (`#pipeline-health-chart`), full-width yield card (`#agency-yield-chart`);
  loads vendored Chart.js + `stats-chart.js`; placeholder KPI row removed.
- `core-decision-dotnet/wwwroot/scripts/stats-chart.js` (new) — fetches
  `/report/statsfull` with the two filter inputs; S7 horizontal funnel bars
  with an All/agencies dropdown switching datasets in place, S1 horizontal
  rate bars (x 0–100, raw counts in tooltip `afterBody`), S8 two-series bar
  with `MM-DD` labels; theme change rebuilds charts from the cached payload
  (same MutationObserver pattern as `dashboard-chart.js`); no auto-refresh.
- `core-decision-dotnet.Tests/JobStatsTests.cs`, `ReportStatsTests.cs` —
  see Tests.
- `docs/DASHBOARD_CHARTS.md` — §6 roadmap step 4 marked done.

## Decisions recorded

- **Funnel stage 3 = Accepted** (Attention + Applied), per "definitions as
  S1" — keeps the funnel monotone (jobs currently Applied passed through
  Attention). Named `Attention` in the DTO/labels for the doc's vocabulary.
- **One SQL serves S1 and S7**: the funnel is derived in C# from the yield
  rows (overall = sums), so `statsfull` issues a single agency-aggregate
  query.
- **Filters**: agencies (title IN) applies to S1/S7/S8; countries (Job.Country
  IN) applies to S8 only — S1/S7 have no Country dimension (§4 S1 note).
  Same parameterized `@a{i}`/`@c{i}` convention as `Fetch`.
- **Zero-job agencies**: kept in S1 (LEFT JOIN, rates 0 — matches the
  dashboard agency report) but excluded from the S7 per-agency list to keep
  the dropdown useful; the overall funnel always includes them.
- **Rates in C#** (`(long)(100.0 * value / total)`) instead of SQL CASE —
  same truncation as `Q_JOB_RATE_REPORT`, less SQL to keep in sync.

## Tests

`JobStatsTests` (GoldenDatabase, `Seed` extended with agency/country):
yield counts/rates across states and agencies (Analyzed excludes Saved;
Accepted = Attention + Applied; 5 jobs → 60%/66%); yield agency-title filter;
funnel overall sums + per-agency stages + zero-job agency excluded; health
buckets AiPending/AIError by ModifiedOn day (other states and out-of-window
rows ignored) with 30-day zero-fill; health agency+country filters; empty-DB
zero-fill. `ReportStatsTests.StatsFull_without_filters_returns_filled_sections`
replaces the empty-arrays test: envelope sections filled, zero-job agency
row, zero overall funnel, 30 zero health rows ending today.

## Must not

S2–S6/S9/S10 datasets (roadmap steps 5–6), `docs/API.md` entries, status
header flip, TODO.md checkbox, no auth/middleware changes. The 3
`LinkedInMarkupTests` failures are pre-existing (gitignored `/examples/`
fixtures missing locally).

## Validate

```
dotnet build Job-Seeker.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

Manual smoke (`dotnet run --project core-decision-dotnet`): `/report/stats`
renders the funnel (All + per-agency via dropdown), yield bars and health
chart on first load; Agencies/Countries + Apply refetch both the page and
`/report/statsfull?...` with filtered data; theme toggle recolors ticks and
legends.
