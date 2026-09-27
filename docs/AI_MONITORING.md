# AI worker monitoring

> Observability for `ai-worker` per the 2026-09-23 monitoring-program lock in
> [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md). Two-tier data contract:
> **aggregates travel to the core, raw debug material stays local** on the AI
> station. The 2026-09-25 deferred upgrades were implemented 2026-09-27 (see
> [§ 2026-09-27 upgrades](#2026-09-27-upgrades-implemented)).

## Panel

Read-only Razor page in the core dashboard: **`/monitor`** (also linked as
"AI Monitor" on the home page). Server-rendered, auto-refreshed every 15 s
(the body partial is swapped in via `GET /monitor/body`), English, no
control buttons — Re-queue/Promote stay
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
3. **Success by stage** — per-group job counts by `State` plus derived
   rates — regex pass (AI-lane / AI-lane + `NotApprovedRegex`), AI promote
   (`Attention` / judged outcomes), disposition
   (`Applied`+`Rejected` / `Attention`); zero denominators render `n/a`.
   Grouping via query param (`/monitor?group=agency|country`, agency
   default; invalid → agency): country from `Job.Country` (empty →
   `(none)`), agency via an `Agency.Title` join; plain toggle links, no JS
   routing. A Chart.js stacked bar (x = group, segments = `State`) mirrors
   the table; the table stays authoritative.
4. **Helper blocks** — verdict distribution (counts per `AiVerdict`, Error
   its own slice; compares model strictness against the 85/70/50 bands and
   the 60 passmark), AiPending age buckets (≤6h / 6–24h / 1–3d / >3d over
   `RegTime`; queue staleness beyond the oldest-pending number), and run
   trends (last 30 runs: jobs per run + avg AI wait per call as two lines;
   spots GPU/model slowdown and prompt growth).
5. **Run history** (last 30 `AiRun` rows) — start time, derived `s/job`
   (mean wall / jobs; totals in tooltip), exit code,
   jobs/promoted/errors/404/retries, `Max tok/call` (max prompt+completion
   of a single LLM call; totals in tooltip), `AI wait` (mean per-call
   latency `CallMs / Calls` with `Calls = Jobs + Promoted + Retries`; max
   call latency + totals in tooltip), derived tok/s, model, worker
   version, rubric hash; `errorJobIds` link to job-detail. The Run-column
   tooltip carries `llmFailures`, `finishReasonLength`, `truncatedJobs`,
   `droppedMemoryRows`. "—" marks pre-update rows / an old worker.

Every metric block carries what/why/action notes in the page itself.
Charts are rebuilt on every 15 s swap: the body partial embeds
`<script type="application/json">` data blocks (readable via `textContent`
after an `innerHTML` swap — inline scripts never execute) and
`wwwroot/scripts/ai-monitor-chart.js` destroys + recreates the Chart.js
instances via the `window.initMonitorCharts()` hook called by
`server-operations.js` after each swap and once on page load.

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
`workerVersion` (optional, ≤ 64 chars — the worker's informational version
`semver[+hash]`; shown as the Worker column in `/monitor` Run history, `—`
when absent), `temperature`, `seed`, `rubricHash`, `rubricTailorHash`
(first 10 hex chars of SHA-256), `jobs`, `promoted`, `errorVerdicts`,
`gone404`, `retries`, `llmFailures`, `promptTokens`, `completionTokens`,
`maxCallTokens`, `callMs`, `maxCallMs` (both optional since worker 1.2.0;
absent → 0 → "—" / 0-based tooltips), `wallSeconds`,
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
- `MaxCallTokens` / `MaxCallMs` (2026-09-27) ship via the SchemaUpdate
  mechanism: `database/updates/20260927-04-ai-run-max-call-metrics.sql`
  (two additive `ALTER TABLE`s, auto-applied at startup).
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

## 2026-09-27 upgrades (implemented)

> Everything designed 2026-09-25 was built on 2026-09-27 with the
> modifications below (decision log 2026-09-27). The 2026-09-25 entries are
> superseded where noted.

- **Run history** — `Duration` became `s/job` (mean `WallSeconds / Jobs`,
  totals in tooltip); the run-total "Tokens in/out" column became
  **`Max tok/call`** (`maxCallTokens`); a new **`AI wait`** column shows the
  mean per-call latency `CallMs / Calls` with the max single-call latency
  (`maxCallMs`) and totals in the tooltip. `maxCallTokens`/`maxCallMs` are
  new Tier-1 fields stored via two additive `AiRun` columns — **reversing
  the 2026-09-25 max-drop lock** (the schema-addition objection fell with
  the 2026-09-27 `WorkerVersion` precedent). Token per-job/per-call
  averages were dropped (user); no percentiles — per-run call counts are
  too small, mean + max covers typical + tail, the full distribution stays
  in the local Tier-2 logs. Retries count toward the max metrics
  (usage-bearing); null-usage calls are skipped for `maxCallTokens` but
  their `ElapsedMs` counts for `maxCallMs`; 0 renders as "—". The call
  count is derived, not stored: `Calls = Jobs + Promoted + Retries`.
  `tok/s` unchanged. The Run-column tooltip now carries `llmFailures`,
  `finishReasonLength`, `truncatedJobs`, `droppedMemoryRows` — closing the
  old "hover the Run column for totals" promise.
- **Success by stage** — implemented as designed (query-param group
  switch, plain links, stacked Chart.js bar, table authoritative).
- **Helper blocks** — verdict distribution, AiPending age buckets
  (`julianday` arithmetic over `RegTime`, local-time naive text — the same
  parseability Queue health relies on), run trends (jobs per run + avg AI
  wait **per call**, derived from the existing `Runs` list, oldest →
  newest, no new SQL).
- **Notes coverage** — `/monitor` notes per block; the dashboard
  (`index.cshtml`) gained one compact What/Why/Action note under each of
  the three sections (jobs, trends, agencies) — the blocks live in
  `index.cshtml`, never in the row partials (invalid HTML there, and
  wiped every 15 s) — plus short `title` tooltips on chart cards,
  buttons and KPI chips.
- **AI-queue job count placement** — standing no-op decision: it stays
  solely in Queue health (the 2026-09-23 page-role lock stands; no
  dashboard-digest duplication).

## Where the code lives

| Piece | File |
|-------|------|
| Run payload + rubric hash | `ai-worker/RunReport.cs` |
| Run identity, retention, summary JSON, failure dumps | `ai-worker/WorkerLog.cs` |
| Counters (`maxCallTokens`, `maxCallMs`, `finishReasonLength`, `truncatedJobs`, `droppedMemoryRows`, `errorJobIds`) | `ai-worker/RunStats.cs` |
| Report POST (warn + one retry) | `ai-worker/CoreClient.cs` `PostRunReportAsync` |
| Report on every exit path | `ai-worker/WorkerLoop.cs` `RunAsync`/`Summary` |
| Endpoint | `core-decision-dotnet/Controllers/Ai.cs` `RunReport` |
| Table + upsert | `database/structure/ai-run.sql`, `database/updates/20260927-04-ai-run-max-call-metrics.sql`, `Database/Business/AiRunBusiness.cs` |
| Panel | `Controllers/Monitor.cs`, `Views/ai-monitor.cshtml`, `Database/Business/JobBusiness.Monitor.cs` |
| Stages + helper SQL | `Database/Business/JobBusiness.MonitorStages.cs` |
| Stages/helper sections (table, canvases, JSON blocks) | `Views/ai-monitor-stages.cshtml` (included by `ai-monitor-body.cshtml`) |
| Monitor charts (destroy + rebuild each refresh) | `wwwroot/scripts/ai-monitor-chart.js` (`window.initMonitorCharts` hook; swap in `server-operations.js`) |
