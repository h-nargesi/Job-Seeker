# Dashboard charts and stats page

> **Status: design proposal — not implemented.** Records the chart/report
> design session of 2026-09-21: which charts the dashboard gets, which live
> on a separate stats page, and the exact data semantics decided for each.
> No chart code exists yet.

## 1. Decision: one chart on the dashboard, the rest on a stats page

The dashboard (`Views/index.cshtml`) stays a **control/monitor console**:
light to load, partial-refresh friendly, within the ~400-line file budget.
Chart.js, heavy aggregates, and analysis views live on a new page
(`/report/stats`, view + JSON endpoints under the existing dashboard auth),
reached from a "Charts" button next to the `Reset Trends / Revaluate / ...`
button row.

```
/ (dashboard)                    /report/stats (new page)
┌──────────────────────┐        ┌────────────────────────────────┐
│ Job List  (existing) │        │ Velocity (Applied/Rejected/day)│
│ Trend List (existing)│        │ + KPI: avg disposition days    │
│ ...                  │        │ + Attention backlog            │
├──────────────────────┤        ├────────────────────────────────┤
│ Daily stacked bar    │        │ Agency yield │ Score histograms │
│ by State (chart 1+2) │        ├────────────────────────────────┤
└──────────────────────┘        │ Skills gap   │ Scatter (exp x)  │
                                │ AI donuts (filtered)            │
                                └────────────────────────────────┘
```

Rationale: load weight (Chart.js only on the stats page), page roles
(real-time control vs periodic analysis), and `index.cshtml` file budget
(prefer a partial for the dashboard chart section if it grows).

## 2. Date semantics (decided)

- `RegTime` = when the job was scraped/registered — the x-axis for
  registration-volume charts (`SUBSTR(RegTime, 1, 10)` as day).
- `ModifiedOn` = last state change — the **disposition date** for
  `Applied`/`Rejected`. Velocity charts must bucket by `ModifiedOn`, never
  `RegTime`; otherwise resolution speed is overstated for old scraped jobs.

## 3. Dashboard: daily stacked funnel + velocity (charts 1+2, merged)

One query family, two views:

- **Daily stacked bar by State** — x = day, bar segments = current
  `State` counts of jobs registered that day. Shows both daily intake and
  its outcome mix (the state funnel unrolled over time). A day × State
  heatmap is the compact alternative if the range grows.
- **Velocity line** — `Applied` and `Rejected` counts per day bucketed by
  `ModifiedOn`, as two separate lines (different meanings).
- **KPI tiles** beside the charts:
  - avg disposition time: `AVG(JulianDay(ModifiedOn) - JulianDay(RegTime))`
    over `State IN (Applied, Rejected)`;
  - Attention backlog: count and avg age
    (`JulianDay('now') - JulianDay(RegTime)`) of open `Attention` jobs.

## 4. Stats page charts

| # | Chart | Data / filter | Notes |
|---|-------|---------------|-------|
| 3 | Agency yield (horizontal bar) | existing `AgencyBusiness.JobRateReport` (`JobCount/Analyzed/Accepted/Applied`, `AnalyzingRate`, `AcceptingRate`) | no new SQL; reuse the dashboard agencies query |
| 4 | Score histograms (dual) | `Score` and `AiScore` distributions | calibrates `job-option.sql` weights and revaluation thresholds |
| 5 | AI donuts | `AiWorkModel`, `AiRelocation`, `AiSeniority`/`AiContract` — **filtered to jobs that passed both gates**: `State IN (Attention, Applied, Rejected)` ("sufficient final score" proxy; a numeric `AiScore >= N` filter is a later option) | |
| 7 | Market skills gap (horizontal bar, top 20–30) | tokenize `AiSkills` across **all analyzed jobs** (whole market, not only matched ones), aggregate in C#/LINQ, then match each skill against the user's own keyword list (from `job-option.sql` / `ResumeContext`) | bars colored have/missing — the missing ones are the learning list; optional weekly frequency variant to surface *rising* skills. SQLite `json_each` is possible but C# aggregation is preferred for readability |
| 8 | Scatter | x = `AiExperienceYears`, y = effective score — reuse `JobRanking.SqlRankScore` + the decay CASE already mirrored in `Q_INDEX` (keep in sync) | shows at which seniority level the profile is competitive |

Rejected in review: the salary-range chart (`AiSalaryMin/Max` normalized by
`AiCurrency`/`AiPeriod`) — explicitly declined by the user, do not add.

## 5. Implementation notes

- Endpoints: JSON actions on `ReportController` (e.g. `StatsDaily` — last
  ~30 days, for the dashboard chart — and `StatsFull` for the stats page).
  Chart.js consumes arrays, not Razor partials.
- New SQL lives in `JobBusiness.Sql.cs` (currently well under budget) as
  `GROUP BY` queries; `State` must compare against the enum **name as text**
  via `nameof(JobState.X)` (repo-wide rule).
- Frontend: Chart.js via CDN or vendored under `wwwroot/`; the stats page
  reuses `layout.cshtml` and loads Chart.js only in its own script block.
- Auth: the stats page and JSON endpoints sit under the existing dashboard
  role like the other `Report` views.
