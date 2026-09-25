# AI worker monitoring

> Observability for `ai-worker` per the 2026-09-23 monitoring-program lock in
> [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md). Two-tier data contract:
> **aggregates travel to the core, raw debug material stays local** on the AI
> station. Agreed-but-unbuilt upgrades from the 2026-09-25 session live in
> [§ Deferred upgrades](#deferred-upgrades-2026-09-25--design-only-not-implemented).

## Panel

Read-only Razor page in the core dashboard: **`/monitor`** (also linked as
"AI Monitor" on the home page). Server-rendered, manual refresh only (the
dashboard polls nothing), English, no control buttons — Re-queue/Promote stay
on job-detail. Sections:

1. **Queue health** — `AiPending` / `AIError` counts, oldest pending age.
   Growing pending with no new runs = worker not being started; growing
   `AIError` = model keeps failing.
2. **Calibration** (live SQL over `Job`) — verdict-band mismatch (stored
   verdict vs the 85/70/50 score bands), inverted salaries
   (`AiSalaryMin > AiSalaryMax`), keyword-rich-jobs-with-no-skills
   (regex `Score >= floor` but empty `AiSkills`), and per-field
   NULL/`Unknown` coverage for every extraction field. High values mean the
   rubric or schema needs tightening.
3. **Run history** (last 30 `AiRun` rows) — start time, wall duration, exit
   code, jobs/promoted/errors/404/retries, tokens in/out, derived tok/s,
   model, rubric hash; `errorJobIds` link to job-detail.

Every metric block carries what/why/action notes in the page itself.

## Report contract (`POST /ai/run-report`)

The worker posts one JSON payload on **every** exit path (including aborts,
interrupts and unexpected exceptions — best effort before a rethrow). Error
policy: warn + one retry, never aborts the run; the local JSON stays the
source of truth. The core upserts `INSERT OR REPLACE` by `RunID`, so
retries/re-posts are harmless. Worker-key auth (`/ai/*`); no other client may
call it. Validation mirrors `AiVerdictRequest` style (ranges/caps); invalid
payloads get 400.

Tier-1 fields (camelCase JSON, stored in the `AiRun` table):

`runId`, `startedUtc`, `finishedUtc` (ISO text), `exitCode` (0 ok, 1 core
abort, 2 llm unavailable, 3 interrupted, 5 unexpected), `model`,
`temperature`, `seed`, `rubricHash`, `rubricTailorHash` (first 10 hex chars
of SHA-256), `jobs`, `promoted`, `errorVerdicts`, `gone404`, `retries`,
`llmFailures`, `promptTokens`, `completionTokens`, `callMs`, `wallSeconds`,
`finishReasonLength`, `truncatedJobs`, `droppedMemoryRows`, `errorJobIds`
(JSON array, cap 50).

Tier 2 (never transferred): full prompt bodies, raw model outputs / failure
dumps, per-call records, llama-server internals.

## Local log layout (AI station)

- `logs/runs/{runId}.log` — per-run Serilog sink at **Debug** (full prompt
  bodies). `runId` = `yyyyMMdd-HHmmss`, `-<pid>` suffix on same-second
  collision.
- `logs/runs/{runId}.json` — the exact Tier-1 payload written at summary,
  including the exit code. Source of truth when the POST failed.
- `logs/W.log` (daily rolling) — unchanged aggregate worker log.
- `logs/failures/{runId}/job-{jobId}-call{n}-attempt{a}.txt` — raw invalid
  model outputs, filed per run.
- Retention: the newest **30** run logs (`.log` + `.json` pairs) are kept;
  older ones are pruned at startup. Failure dumps are not pruned
  automatically.

## Schema and one-time ops

- `database/structure/ai-run.sql` — additive `CREATE TABLE IF NOT EXISTS
  AiRun (...)`, listed explicitly in `database/installation.sh`.
- Existing populated databases need the one-time manual create (a new table
  is not an ALTER; no migration framework):

  ```bash
  sqlite3 data.sqlite3 < structure/ai-run.sql
  ```

- Salary inversion cleanup (validation ships in both `VerdictParser.Parse`
  and `AiVerdictRequest.TryCreate`; this one-off fixes historical rows —

  count first, then swap):

  ```sql
  SELECT COUNT(*) FROM Job
  WHERE AiSalaryMin IS NOT NULL AND AiSalaryMax IS NOT NULL
    AND AiSalaryMin > AiSalaryMax;

  UPDATE Job SET AiSalaryMin = AiSalaryMax, AiSalaryMax = AiSalaryMin
  WHERE AiSalaryMin IS NOT NULL AND AiSalaryMax IS NOT NULL
    AND AiSalaryMin > AiSalaryMax;
  ```

  Run against the production DB on 2026-09-23: 0 inverted rows — no swap
  needed.

## Deferred upgrades (2026-09-25 — design only, not implemented)

> Locked with the user on 2026-09-25; implementation deliberately deferred
> to a later session. Nothing in this section exists in code yet. Decisions
> are also recorded in the decision log (2026-09-25).

### Run history — derived metrics (no schema change)

- Replace the run-total "Tokens in/out" cell with **averages**: avg
  input/output tokens **per job** (`PromptTokens / Jobs`,
  `CompletionTokens / Jobs`) and **per call**, and add an **AI wait**
  column (avg LLM response latency per job, `CallMs / Jobs`; per-call
  average and total call time in the tooltip). Run totals move into
  tooltips.
- Per-call averages need a call count. It is **derived, not stored**:
  `Calls = Jobs + Promoted + Retries` — every parse retry returns a
  usage-bearing response whose tokens are already summed
  (`RunStats.Add` runs in `LogCall` before parsing), and
  connection-level failures (`LlmFailures`) return no usage and never
  increment `Retries`.
- **Max(prompt + completion) per call is dropped** — not derivable
  without a new counter, and the user declined the `AiRun` schema
  addition (no new columns, no Tier-1 payload change of any kind).
- Surface the stored-but-never-displayed counters — `llmFailures`,
  `finishReasonLength`, `truncatedJobs`, `droppedMemoryRows` — in the
  Run-column tooltip, closing the panel's currently broken "hover the
  Run column for totals" promise (the note text exists today; the
  tooltip lacks the totals).

### Success by stage — agency/country switch + stacked state chart

- New panel section: per-group job counts by `State` plus derived rates
  — regex pass (AI-lane states / AI-lane + `NotApprovedRegex`), AI
  promote (`Attention` / judged outcomes), disposition
  (`Applied`+`Rejected` / `Attention`); zero denominators render `n/a`.
- Grouping via query param (`/monitor?group=agency|country`, agency
  default): country from `Job.Country` (empty → `(none)`), agency via a
  `Agency.Title` join. Server-side switch rendered as toggle links; no
  JS routing.
- **Chart:** Chart.js stacked bar — x = group label, segments = `State`
  counts (same rows as the table; the table stays authoritative).
  Chart.js 4.x UMD vendored at `wwwroot/scripts/chart.umd.js` (no CDN,
  no build step, per `DASHBOARD_CHARTS.md` implementation notes), loaded
  only by the monitor view; init in
  `wwwroot/scripts/ai-monitor-chart.js`.

### Helper blocks

1. **Verdict distribution** — counts per `AiVerdict` (Error its own
   slice); compares model strictness against the 85/70/50 bands and the
   60 passmark.
2. **AiPending age buckets** — ≤6h / 6–24h / 1–3d / >3d over `RegTime`
   for `State = AiPending`; queue staleness beyond the single
   oldest-pending number; tells the user when to start the worker.
3. **Run trends** — last 30 runs: jobs per run and avg AI wait per job
   as two lines; spots GPU/model slowdown and prompt growth over time.

### Notes coverage

- `/monitor` keeps What/Why/Action notes on every block (house style,
  including run-history column tooltips). The other report pages
  (`jobs`, `trends`, `agencies`) gain one compact note block each; the
  **dashboard** (`index.cshtml`) gets short `title` tooltips only
  (user decision: the dashboard stays a terse control console).

### AI-queue job count placement

- The number of jobs waiting for a verdict is **already displayed** in
  Queue health (`AiPending` + oldest pending age). Decision: it stays
  there as the single source — no dashboard duplication (the 2026-09-23
  page-role lock stands). Surfacing it on the dashboard digest would be
  a new decision superseding that lock.

## Where the code lives

| Piece | File |
|-------|------|
| Run payload + rubric hash | `ai-worker/RunReport.cs` |
| Run identity, retention, summary JSON, failure dumps | `ai-worker/WorkerLog.cs` |
| Counters (`finishReasonLength`, `truncatedJobs`, `droppedMemoryRows`, `errorJobIds`) | `ai-worker/RunStats.cs` |
| Report POST (warn + one retry) | `ai-worker/CoreClient.cs` `PostRunReportAsync` |
| Report on every exit path | `ai-worker/WorkerLoop.cs` `RunAsync`/`Summary` |
| Endpoint | `core-decision-dotnet/Controllers/Ai.cs` `RunReport` |
| Table + upsert | `database/structure/ai-run.sql`, `Database/Business/AiRunBusiness.cs` |
| Panel | `Controllers/Monitor.cs`, `Views/ai-monitor.cshtml`, `Database/Business/JobBusiness.Monitor.cs` |
