# Dashboard charts and stats page

> **Status: design finalized 2026-09-26 — implementation not started;
> decisions and roadmap below.** Supersedes the 2026-09-21 proposal; no
> chart code exists yet. Chart IDs use the D/S scheme (D = dashboard,
> S = stats page), replacing the old 3–8 numbering.

## 1. Page split

The dashboard (`Views/index.cshtml`) stays a **control/monitor console**:
light to load, partial-refresh friendly, within the ~400-line file budget.
Chart.js, heavy aggregates, and analysis views live on a new stats page,
reached from a "Charts" button next to the `Reset Trends / Revaluate / ...`
button row.

```
/ (dashboard)                     /report/stats (new page)
┌───────────────────────┐        ┌─────────────────────────────────┐
│ Job List   (existing) │        │ D3 KPI tiles · D2 velocity      │
│ Trend List (existing) │        ├─────────────────────────────────┤
│ ...                   │        │ S7 funnel · S1 yield · S8 AI    │
├───────────────────────┤        ├─────────────────────────────────┤
│ D1 daily stacked bar  │        │ S2 histograms · S3 donuts ·     │
│ (auto-refresh 15 s)   │        │ S4 skills · S5 · S6 · S9 · S10  │
└───────────────────────┘        └─────────────────────────────────┘
```

Rationale: load weight (Chart.js only on the stats page), page roles
(real-time control vs periodic analysis), and the `index.cshtml` budget
(dashboard chart lives in a partial, `dashboard-chart.cshtml`).

## 2. Decided semantics

Rules 1–2 are **decisions the future implementation must apply — the
current code does NOT behave this way yet**:

1. **ModifiedOn guard (future code change).** Non-state-change UPDATEs must
   not touch `ModifiedOn` when `State IN (Applied, Rejected)`. Affected
   queries in `Database/Business/JobBusiness.Sql.cs`: `Q_UPDATE_CONTENT`,
   `Q_REGISTER_ATTEMPT`, `Q_CHANGE_OPTIONS`, `Q_SAVE_RESUME_TEXT`,
   `Q_REMOVE_HTML`, plus the dynamic UPDATE in `UpdateScrapedJob`. Pattern:
   `ModifiedOn = CASE WHEN State IN ('Applied','Rejected') THEN ModifiedOn
   ELSE @now END`. State-change queries (`Q_CHANGE_STATE`, `Q_MANUAL_STATE`,
   `Q_MARK_APPLIED`) keep writing `ModifiedOn` unconditionally. Rationale:
   `/job/options` and `/job/resumetext` can currently bump `ModifiedOn` on
   an Applied job and corrupt its disposition time.
2. **Local-time rule (future code change).** All system-written Job
   timestamps become server-local: `job.sql` defaults change from
   `current_timestamp` (UTC) to `datetime('now','localtime')`; Job INSERTs
   set `RegTime`/`ModifiedOn` explicitly with `@now = DateTime.Now`. Stats
   SQL must never use SQLite `'now'` (UTC) — `@now` is passed from C#.
   Legacy rows keep their UTC skew; explicitly **no migration**.
   (`trend.sql`/`memory.sql` defaults: optional follow-up, out of scope.)
3. **Age semantics.** Every age/disposition computation uses
   `COALESCE(PublishedAt, RegTime)` — aligned with `AgeDays` in `Q_INDEX`.
   (The earlier draft's bare-`RegTime` KPI formula was a bug.)
4. **Velocity semantics.** Applied/Rejected counts are bucketed by
   `ModifiedOn` (= disposition time — valid once rule 1 lands; until then
   non-state writes can still shift it, see rule 1).

## 3. Dashboard charts (D1–D3)

- **D1 — daily stacked bar by State.** x = day (`SUBSTR(RegTime, 1, 10)`),
  7 segments: `Saved` / `Revaluation` / gate-rejected (`NotApprovedRegex` +
  `NotApprovedAI`) / in-AI (`AiPending` + `AIError`) / `Attention` /
  `Applied` / `Rejected`.
- **D2 — velocity lines.** `Applied` and `Rejected` counts per day bucketed
  by `ModifiedOn`, two separate lines (§2 rule 4).
- **D3 — KPI tiles.**
  - avg disposition days:
    `AVG(JulianDay(ModifiedOn) - JulianDay(COALESCE(PublishedAt, RegTime)))`
    over `State IN (Applied, Rejected)`;
  - Attention backlog: count + avg age
    `JulianDay(@now) - JulianDay(COALESCE(PublishedAt, RegTime))` — note the
    `@now` parameter from C#, never SQLite `'now'`.

## 4. Chart catalog

| ID | Chart | Page | Data value → decision |
|----|-------|------|-----------------------|
| D3 | KPI tiles (avg disposition days; Attention backlog count + avg age) | dashboard | How far behind am I, how fast do I decide → prioritize clearing backlog today |
| D1 | Daily stacked bar by State (7 segments) | dashboard | Daily intake volume + outcome mix over time → is intake healthy, is the mix shifting |
| D2 | Velocity lines (Applied/day, Rejected/day) | dashboard | Decision throughput trend → detect stalling, keep daily apply discipline |
| S7 | Funnel Saved→Analyzed→Attention→Applied (+ per agency) | stats | Where the pipeline leaks → fix scoring keywords or platform choice |
| S1 | Agency yield horizontal bar | stats | Which platforms yield accepted jobs → allocate scraping effort |
| S8 | Pipeline health daily (AiPending backlog, AIError/day) | stats | AI-stage regressions early → fix prompt/model quickly |
| S4 | Skills gap top 20–30, have/missing colored | stats | Market-demand skills vs own skills → learning list; add JobOption keywords |
| S2 | Score histograms ×2 (normalized Score + AiScore, AiPassmark line) | stats | Score distribution vs thresholds → tune weights/passmark/patterns |
| S3 | AI donuts ×4 (WorkModel, Relocation, Seniority, Contract) | stats | Market composition of the competing segment → search-filter choices |
| S6 | AiVerdict donut (StrongMatch/Match/Possible/NoMatch/Error) | stats | AI strictness and health → adjust AiPassmark or prompt |
| S5 | Competitiveness by experience bucket (% gate-passed per bucket) | stats | At which seniority the profile is competitive → target experience ranges |
| S9 | Attention aging buckets (0–2, 2–7, 7–14, >14 d) | stats | Backlog age distribution → act on stale Attention jobs |
| S10 | Disposition-time distribution (histogram + median/P90) | stats | Typical and worst time-to-decide → process discipline |

Rejected in review (do not add): salary-range chart (`AiSalaryMin/Max`
normalized by `AiCurrency`/`AiPeriod`) — explicitly declined by the user.

### Stats-page chart specs

- **S1 — agency yield.** Reuse `AgencyBusiness.JobRateReport`
  (`JobCount/Analyzed/Accepted/Applied`, `AnalyzingRate`, `AcceptingRate`;
  Analyzed = rows with `State != 'Saved'`, Accepted = `Attention + Applied`).
  The stats-page `agencies` filter applies; the `countries` filter does not
  (the query has no Country dimension). Cleanup drift (rates change as old
  rows are deleted) is accepted.
- **S2 — score histograms ×2.** Distribution histograms, **no time axis**:
  normalized `Score` via `JobRanking.SqlRegexNorm` (needs `@scoreCap`) and
  `AiScore`; 10-point bins; vertical `AiPassmark` line on the AiScore panel;
  N (non-null count) displayed.
- **S3 — AI donuts ×4** (2×2 grid): `AiWorkModel`, `AiRelocation`,
  `AiSeniority`, `AiContract`; population = gate-passed jobs
  `State IN (Attention, Applied, Rejected)`.
- **S4 — skills gap.** Population = all rows with `AiSkills IS NOT NULL`
  (whole market, not only matched jobs). "Have" reference = union of
  master-resume `ResumeContext.Keys` phrases + key names, AND regex match of
  the normalized skill phrase against `JobOption.Pattern` where
  `Category IN ('field','tech','resume')` (excluded: `benefit`, `salary`,
  `keywords`, `reject`, and `production` — ERP packages, not skills).
  Normalization: lowercase, trim, strip extra punctuation, plural fold, then
  an editable alias map (AppSetting key `SkillAliases`, JSON
  alias→canonical, code-level seed when absent). Counting per normalized
  phrase (multi-word skills stay whole); matching exact post-normalization.
  Aggregation in C#/LINQ, not SQLite `json_each`. Optional weekly "rising
  skills" variant: deferred.
- **S5 — competitiveness by experience bucket** (replaces the old scatter).
  x = experience buckets (0–2, 3–5, 6–9, 10–14, 15+), y = % of jobs in
  bucket that passed both gates; optional second series: avg effective score
  via the shared `JobRanking.SqlEffectiveScore` constant (AgeDays CTE
  mirroring `Q_INDEX`; reusing the shared constant keeps it in sync by
  construction). Calibration-snapshot caveat: values are computed with
  current weights at render time.
- **S6 — AiVerdict donut** over all rows with `AiVerdict IS NOT NULL`
  (health view, not market view — deliberately different population from S3).
- **S7 — funnel** Saved→Analyzed→Attention→Applied, overall + per agency
  (definitions as S1).
- **S8 — pipeline health daily.** `AiPending` backlog count + `AIError`
  counts per day (`SUBSTR(ModifiedOn, 1, 10)`).
- **S9 — Attention aging buckets** 0–2 / 2–7 / 7–14 / >14 days, with `@now`.
- **S10 — disposition-time distribution.** Day-diffs for Applied/Rejected
  (§2 rules 3–4); histogram buckets (e.g. 0–1, 2–3, 4–7, 8–14, 15+) plus
  median and P90 (percentiles computed in C#).

## 5. Implementation notes

- **Endpoints:** `GET /report/stats` (page), `GET /report/statsdaily`
  (dashboard JSON, last 30 days, no filters), `GET /report/statsfull`
  (stats-page JSON; accepts `agencies`/`countries` — same convention as
  `ReportController.Jobs`). Auth: existing middleware already covers these
  paths (dashboard role) — no auth changes.
- **Chart.js:** vendored, pinned v4 UMD under `wwwroot/scripts/lib/` — no CDN.
- **Refresh:** dashboard charts auto-refresh every 15 s (same pattern as
  `server-operations.js` `setInterval(..., 15000)`); the stats page reloads
  manually only. Chart refresh fetches **JSON data only** — the chart JS
  updates its datasets in place; charts are never refreshed by loading HTML
  (no partials, no page reloads).
- **File budgets:** dashboard chart in a partial (`dashboard-chart.cshtml`)
  + external JS; stats endpoints in a `Report.Stats.cs` partial controller;
  stats SQL as `Q_STATS_*` in `JobBusiness.Sql.cs` + methods in a
  `JobBusiness.Stats.cs` partial.
- **SQL rules (repo-wide):** enum names as text via `nameof(JobState.X)`;
  `@now` from C#, never SQLite `'now'`.
- **Indexing:** the schema currently ships no secondary indexes (no
  `CREATE INDEX` in `database/structure/`). When the stats endpoints land,
  ship a `database/updates/YYYYMMDD-NN-indexes.sql` script
  (`CREATE INDEX IF NOT EXISTS ...`) covering the hot paths: `Job(State)`
  and `Job(AiVerdict)` for the monitor queue/calibration scans,
  `Job(ModifiedOn)` and `Job(RegTime)` for the daily buckets, and
  `AiRun(StartedUtc DESC, RunID DESC)` for `AiRunBusiness.Recent`. Verify
  with `EXPLAIN QUERY PLAN` on `Q_STATS_*`, `Q_INDEX`, and the monitor
  queries once real data accumulates.

## 6. Implementation roadmap (not started)

Priority order: D3, D1, D2, S7, S1, S8, S4, S2, S3, S6, S5, S9, S10.
Implementation chats continue the `docs/impl/CHAT-XX.md` numbering.

1. **Data foundations:** ModifiedOn guard + local-time writes; xUnit tests
   following `JobTimestampsTests.cs` / `CheckpointDatabase.cs`.
2. **Stats infrastructure:** vendored Chart.js, endpoints, views, DTO models.
3. **Dashboard:** D3, D1, D2 + 15 s refresh.
4. **Stats page:** S7, S1, S8.
5. **Skills gap:** S4.
6. **Remaining:** S2, S3, S6, S5, S9, S10.
7. **Wrap-up:** flip this doc's status header; add `docs/API.md` entries for
   the new endpoints.
