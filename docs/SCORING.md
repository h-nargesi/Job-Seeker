# Scoring & state machines

How a job goes from raw HTML to a ranked, resume-ready entry. Core files:
`Analyze/JobEligibilityHelper.cs`, `database/structure/job-option.sql`,
`Database/Business/JobBusiness.cs` (the ranking SQL), and the enums in
`Analyze/Models/`.

## The scoring pipeline (`EvaluateJobEligibility`)

Given a `Job` (with `Content` already extracted from HTML), the engine:

1. **Reset** — clears `Log`, sets `Score = null`.
2. **Expire check** — `JobAcceptabilityChecker` (per-agency regex) tests the
   content. If the job is "no longer accepting applications", it is flagged
   `Expired!` in the log (but still scored).
3. **Language gate** (`LanguageIsMatch`) — tokenizes words ≥3 chars, looks each
   up in the English dictionary (`dictionaries.sqlite3`), and requires **≥50%
   English**. Non-English jobs are rejected. Logs `English: (NN%)`.
4. **Option scoring** (`EvaluateEligibility`) — see below.
5. **Decision**:
   - If the user hasn't already promoted it (`State <= Attention`), set
     `NotApproved` (failed) or `Attention` (passed).
   - If **rejected** OR `Score < MinEligibilityScore` (100) → purge
     `Html`/`Content` (kept only for passing jobs).

## How `JobOption` scoring works

Rules live in `job-option.sql`: each row is `{ Category, Score, Title, Pattern
(regex), Settings (JSON) }`. Every option's `Pattern` is matched against the job
content.

### Categories
| Category | Role |
|----------|------|
| `field` | **Required.** At least one `field` must match, or the job fails outright (`hasField`). Programming languages, stacks. |
| `reject` | **Disqualifying.** Any match rejects the job (e.g. commented-out `react`). |
| `tech` | Supporting technologies (bonus points). |
| `production` | Domain products (e.g. ERP). |
| `benefit` | Perks — notably `Relocation` (170) is also surfaced as a flag on the dashboard. |
| `salary` | Special-cased: score is derived from the parsed amount, not a flat value. |
| `keywords` | Role-level keywords (full-stack, backend, mid-level). |
| `reumse` | Resume-only keywords (AWS, CI/CD); score 0 — they feed the resume context, not the rank. |

### Score accumulation (diminishing returns)
Within a category, matches are grouped by **resume key** (derived from each
option's `Settings.resume`). Scoring then decays:
- The **first** match for a key adds `option.Score × 1.0`.
- Subsequent matches for the **same key** add `option.Score × 0.5`
  (`factor = 0.5F`).
- So repeating the same skill across many options does not inflate the score
  unboundedly.

`MinEligibilityScore = 100`. A job passes only if `hasField && !rejected &&
Score >= 100`.

### Salary scoring (`EvaluateSalaryScore`)
For `salary` options, the score isn't flat: the regex captures a money group and
optionally a period group (`Settings.money` / `Settings.period` are group
indices). Annual salaries are divided by 12; large numbers (>35000) are assumed
annual. Final = `(salary / 1000) × option.Score`.

## Resume context (`ResumeContext`)
Scoring is dual-purpose: the same option matches also populate a
`ResumeContext` (stored as the job's `Options`), which drives resume generation.
Each option's `Settings.resume` says which resume *key* to feed (e.g.
`DOTNET`, `SQL`, `Front-End`) and whether to include the literal matched text.
`ResumeContext.GetKeywords()` builds a short code like `djs` (.NET/Java/SQL)
that names the generated resume file. See `Views/resume.cshtml`.

## State machines

### `JobState` (stored as text in `Job.State`)
```
Saved(1) → Revaluation → NotApproved ─┐
                       ↘ Attention ───┼→ Applied
                       ↘ Rejected
```
- `Saved` — discovered by `SearchPage`, not yet scored.
- `Revaluation` — transient lock set while the re-evaluation pass picks it
  (`JobBusiness.FetchFrom` flips rows to `Revaluation`, then back to `Saved`).
- `NotApproved` — scored, failed the gate.
- `Attention` — scored, passed (≥100). The "review me" pool.
- `Applied` / `Rejected` — terminal user decisions.

> SQL compares against the **name** string, never the int. Queries use
> `nameof(JobState.Attention)` etc. to stay in sync.

### `TrendState` / `TrendType` (workflow progress)
`TrendType` is the coarse type (`Blocked, Login, Search, Job`) derived from
`TrendState`:

| `TrendState` | `TrendType` |
|--------------|-------------|
| Blocked, Finished | Blocked |
| Auth, Login | Login |
| Other, Seeking | Search |
| Analyzing | Job |

`TrendsCheckpoint` uses `(AgencyID, TrendType)` as the unique workflow key. A
trend reserves the right to drive one tab; idle trends get acted on by the
`/decision/orders` poller (open search page → open next job). See the truth
table in `Analyze/TrendsCheckpoint.md`.

### `AgencyStatus` (bitfield, stored in `Agency.Active`)
```
None(0) | ActiveSeeking(1) | ActiveAnalyzing(2) | Active(3)
```
`0` excludes the agency from the active `Agencies` dict (it won't be scraped)
but it stays in `AgenciesByID` for reporting. `1` = actively searching new jobs;
`2` = actively analyzing discovered jobs; `3` = both.

## Ranking (the dashboard sort)

`JobBusiness.Q_INDEX` is the ranked feed for `/report/jobs`. It is a CTE that:

1. Buckets each job by `State` into a `Category` weight
   (`Attention=1, NotApproved=2, Applied/Rejected=4, else=12`).
2. Computes `EffectiveScore = Score × W(age)` where `W` is a piecewise-linear
   **trapezoid** time-weight. Source of truth: `Analyze/JobRanking.cs`.

   | Age (days) | Weight |
   |------------|--------|
   | 0 – 2      | `0.85` (fresh penalty — too-new postings) |
   | 2 – 4      | ramps `0.85 → 1.0` |
   | 4 – 10     | `1.0` (the sweet spot) |
   | 10 – 14    | ramps `1.0 → 0.75` |
   | 14 – 28    | ramps `0.75 → 0.25` |
   | > 28       | `0.15` floor — old jobs sink but never vanish |

   The multiplicative form makes the time term a real modifier of the score
   scale (100–300), not a ±30 tie-breaker, and no job's rank depends on the
   global `MAX(Score)`. `age = JulianDay(MAX(RegTime)) − JulianDay(RegTime)`
   — the clock is the **newest scrape in the table**, so ages don't advance
   while the system is idle. `NULL` Score → NULL EffectiveScore → sorts last
   under `DESC` (unchanged).
3. Flags `Relocation` jobs by matching the log marker `%) Relocation**%`.
4. Partitions by `(AgencyID, State)`, keeps the top N per bucket via an
   explicit CASE cap (`Attention→12, NotApproved→6, Applied/Rejected→3,
   else→1`; the Category 4 bucket orders by `ModifiedOn DESC` first), and
   assigns a final global `Ordering`.

> The SQL `CASE` inside `Q_INDEX` is a **mirror** of `JobRanking.Weight`,
> marked by a keep-in-sync comment. Change the curve in `JobRanking.cs`, the
> SQL mirror, and the breakpoint tests (`core-decision-dotnet.Tests`) together.

Ranking only demotes — real expiry (deleting old jobs) stays exclusively in
`Q_CLEAN`. `GetFirstJob` records the apply-age in `Tries`
(``{n}: {date} (age {N}d)``), which keeps the `'%4: %'` tries-cap pattern
intact and makes the early-application hypothesis measurable later.

Treat this query with care: it references enum names as string literals and the
`Relocation` log format. If you change scoring log output or enum names, the
ranking and the Relocation flag will silently break.

## Maintenance operations

- **`POST /job/revaluate`** — re-run scoring for every job with content (e.g.
  after editing `job-option.sql`). Background, tracked by
  `CurrentRevaluationProcess`.
- **`POST /job/clean`** — retention: delete old non-`Applied` jobs, trim HTML
  from old `Attention`/`NotApproved` jobs, then `VACUUM`.
