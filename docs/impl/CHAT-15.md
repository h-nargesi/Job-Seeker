# Chat 15 — Reports Phase 6: Remaining Charts (S2, S3, S5, S6, S9, S10)

**Status:** done 2026-09-27. **Depends on:** Chat 14 (phase 5). Implements
[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6 roadmap step 6.

## Goal

Add the six remaining stats-page charts to `/report/stats`, all fed by
`GET /report/statsfull`: score histograms (S2), AI market donuts (S3),
AiVerdict health donut (S6), competitiveness by experience (S5), attention
aging (S9), and disposition-time distribution (S10). Manual reload only,
per §5.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §4 chart specs, §5 (SQL
rules: `@now` from C#, enum names via `nameof`; file budgets).

## Files

- `core-decision-dotnet/Database/Business/JobBusiness.Sql.cs` — six new
  queries: `Q_STATS_SCORES` (raw Score/AiScore rows), `Q_STATS_AI_FIELDS`
  (four Ai* columns, gate-passed states), `Q_STATS_VERDICTS` (GROUP BY,
  aliased `AS Verdict` so Dapper name-matching works),
  `Q_STATS_EXPERIENCE` (AgeDays CTE mirroring `Q_INDEX` + shared
  `JobRanking.SqlEffectiveScore`), `Q_STATS_ATTENTION_AGES` and
  `Q_STATS_DISPOSITION_DAYS` (JulianDay day-diffs, `@now` from C#).
- `core-decision-dotnet/Database/Business/JobBusiness.Stats.Analysis.cs`
  (new partial) — `StatsScoreHistograms` (bins normalized Score via
  `JobRanking.RegexNorm` in C#, 10-point bins clamped to bin 9 for 100),
  `StatsAiDonuts` (counts in C# over the four Ai* columns, slices in enum
  order, nulls excluded), `StatsAiVerdict` (slices in `AiVerdict` order),
  `StatsCompetitiveness` (bucket index per row, evaluated/passed counts,
  avg effective score), `StatsAttentionAging`, `StatsDispositionTimes`
  (floor-bucketed diffs; proper median + nearest-rank P90). Shares
  `StatsJobFilter`/`Rate`/`Round` with the existing `JobBusiness.Stats.cs`
  partial.
- `core-decision-dotnet/Analyze/Models/Stats/StatsFullResponse.cs` —
  envelope gained `ScoreHistograms`, `AiDonuts`, `AiVerdictDonut`,
  `Competitiveness`, `AttentionAging`, `Disposition`; new records
  `StatsScoreHistograms`, `StatsDonutChart`, `StatsDonutSlice`,
  `StatsBucketItem`, `StatsBucketList`, `StatsBucketCount`,
  `StatsDisposition`.
- `core-decision-dotnet/Controllers/Report.Stats.cs` — `StatsFull` fills
  the six new sections (both filters apply).
- `core-decision-dotnet/Views/stats.cshtml` — replaced the
  remaining-charts placeholder: S2 ×2 histograms with N captions, S3 four
  donuts (2×2), S6/S9/S10 row, S5 full-width dual-axis bar; footer updated
  (S7/S1 Agencies-only, everything else both filters).
- `core-decision-dotnet/wwwroot/scripts/stats-analysis.js` (new) — makers
  and renderers for the six charts: `passmark_plugin` (dashed vertical
  line at the passmark bin center on the AiScore histogram), generic
  donut/histogram/bucket-bar makers, competitiveness dual-axis (left
  % passed 0–100, right avg effective score), dynamic captions (N counts,
  disposition median/P90). Exposes `render_analysis(data)` and
  `rebuild_analysis_charts()`.
- `core-decision-dotnet/wwwroot/scripts/stats-chart.js` —
  `render_stats_full` dispatches to `render_analysis`, theme rebuild calls
  `rebuild_analysis_charts` (both `typeof`-guarded).
- `core-decision-dotnet.Tests/CheckpointDatabase.cs` — Job DDL gained the
  missing `AiRelocation` column (S3 selects it).
- `core-decision-dotnet.Tests/JobStatsAnalysisTests.cs` (new),
  `ReportStatsTests.cs` — see Tests.

## Decisions recorded

- **Filters:** all six charts follow both Agencies and Countries filters
  (every query is on Job joined to Agency; Job carries Country) — same
  convention as S8/S4.
- **S2 passmark line** is drawn at the center of the bin containing
  `AiPassmark` (category axis; caption states the passmark value).
- **S3 population** = `State IN (Attention, Applied, Rejected)` per spec;
  NULL Ai* values are excluded from the donuts (slices in enum order).
- **S6 population** = all rows with `AiVerdict IS NOT NULL`, including
  informational verdicts on non-queued jobs and `Error` — health view.
- **S5 semantics:** bucket population = rows with `AiExperienceYears >= 0`;
  *evaluated* = `State IN (NotApprovedAI, Attention, Applied, Rejected)`
  (AI reached a verdict outcome; AiPending/AIError excluded — they are
  transient/retried), *passed* = `State IN (Attention, Applied, Rejected)`;
  `PassRate = passed/evaluated` and `AvgEffectiveScore` over evaluated
  rows only, via the shared `JobRanking.SqlEffectiveScore` constant
  (AgeDays CTE mirrors `Q_INDEX`, so calibration-snapshot caveat applies).
- **S9/S10 ages** follow §2 rule 3 (`COALESCE(PublishedAt, RegTime)`);
  S10 day counts are `floor(max(0, diff))` bucketed 0-1/2-3/4-7/8-14/15+;
  median is the true median (mean of middles for even N), P90 is
  nearest-rank.
- **Score histograms** never zero-skip: 10 labels/bins always returned;
  donuts return only present values (empty list on empty DB) and the JS
  skips rendering empty donuts/bars.

## Tests

`JobStatsAnalysisTests`: S2 binning (normalization vs cap, 100-clamp,
over-cap Score, null exclusion, AiPassmark), S3 gate-passed population +
enum-ordered slices + null exclusion, S6 verdict counting across states
(informational Saved verdict included), S5 buckets/pass-rate/avg-score
(negatives and null years excluded, non-evaluated bucket rows don't count,
AgeDays=0 weight 0.85 math asserted), S9 aging buckets (PublishedAt and
RegTime fallback, non-Attention excluded), S10 buckets + median/P90 with
half-day offsets away from bucket edges + empty-DB zeros.
`ReportStatsTests`: all six new sections present, zero-filled on an empty
DB, key/bucket orders asserted.

## Must not

`docs/API.md` entries, DASHBOARD_CHARTS status-header flip, TODO.md
checkbox (all wrap-up, roadmap step 7), no auth/middleware changes. The 3
`LinkedInMarkupTests` failures remain pre-existing (gitignored
`/examples/` fixtures missing locally).

## Validate

```
dotnet build Job-Seeker.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

Manual smoke (`dotnet run --project core-decision-dotnet`): `/report/stats`
renders the two histograms (dashed passmark line on the AI panel), four
market donuts, verdict donut, aging and disposition bars (median/P90 in the
caption), and the dual-axis competitiveness chart; Agencies/Countries +
Apply refilters all of them.
