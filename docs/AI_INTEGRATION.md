# AI integration

> **Status: design proposal + recorded decisions — not implemented.** This
> document consolidates everything about using AI in this system: the decided
> deployment topology (§2), the agreed design for adding an LLM (local-only:
> llama.cpp `llama-server` with a GGUF model such as
> `Qwen3-30B-A3B-Q5_K_M`), the integration points by priority, and the
> decisions taken so far ([`AI_DECISION_LOG.md`](AI_DECISION_LOG.md)). No AI
> code exists yet; nothing here describes current behavior. Amended
> 2026-09-15: the AI lane now runs as a sequential state machine (`AiPending`
> → verdict → `Attention`/`NotApprovedAI`), superseding the earlier parallel
> `AiState`-column design (§4.1 and the decision log). Amended 2026-09-18:
> design-review decisions D1–D10 folded in (revaluation scope + Attempts
> law, per-job Revaluate, verdict fingerprint, `AppSetting` table,
> per-client keys, emergency promote, deterministic verdicts), then the
> same-day second batch D11–D17 (dashboard layout, re-queue source-state
> guard, worker core-error protocol, tailoring rubric, payload formats,
> config cleanup) — see the decision log's 2026-09-18 entries.

Related: [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) (phase 3 detail),
[`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) (phase 5 detail; 5.5 Compose
policy).
Decisions: [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md). Implementation
pointers: [`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md). Execution slices:
[`AI_IMPLEMENTATION.md`](AI_IMPLEMENTATION.md).

## 1. The golden rule: the LLM never sits in the browser loop

`POST /decision/take` is the path the extension blocks on, and the whole
scraping loop is paced by its response time. LLM inference takes seconds;
the loop must stay deterministic and millisecond-fast. So the system gets
two lanes:

```
browser loop (fast, deterministic)          background AI lane (slow, smart)
──────────────────────────────────          ──────────────────────────────────
regex scoring (unchanged)              →    queue on the core → ai-worker on
Job queued with State = AiPending      →   the AI station → verdict, extrac-
                                             tion, resume delta posted back
```

The regex gate stays first and cheap — it rejects most jobs and costs nothing.
The LLM only sees the survivors. This alone cuts the number of model calls by
roughly an order of magnitude versus scoring every scraped job.

The queue lives on the core (DB-backed — §2); the consumer is
`ai-worker`, a separate program on the AI station. The in-core precedent for
a one-by-one background pass with progress tracking is
`JobEligibilityHelper.RunRevaluateProcess`.

**No exception (decided 2026-09-10).** An earlier draft allowed a
"priority claim" so an apply-stage job's resume delta would arrive within
seconds. That mechanism is removed: the core never tracks apply stage (the
human applies manually — see the decision log), and `ai-worker` runs
manually (§2.1), so deltas are produced before a human ever reviews a job.
Nothing in the browser loop ever waits on the model.

## 2. Deployment topology (Phase 0 — decided)

Three stations, two of them outbound-only:

```
                 ┌────────────────────────────────┐
                 │  core: job-seeker (.NET)       │  public, SSL (reverse proxy),
                 │  SQLite + AI queue + API       │  per-client X-API-Key = role
                 └─▲──────────────▲─────────────▲─┘
    POST /decision/take             │            │  GET /ai/next · POST /ai/verdict
    GET  /decision/scopes           │            │  POST /assistant/* (planned —
    (X-Client: search)              │            │  phase 5, X-Client: assistant)
   ┌────────────────────────────────┴─┐        ┌─┴────────────────────────────┐
   │ search terminal (browser host)   │        │ AI station                   │
   │ long-running desktop; logged-in  │        │ GPU box behind NAT — NOT     │
   │ browser + agent-extension;       │        │ reachable from the core;     │
   │ outbound only                    │        │ ai-worker + llama-server     │
   └────────────────┬─────────────────┘        │ (localhost) + assistant      │
                    │ HTTPS                    │ browser (phase 5)            │
                    ▼                          └──────────────────────────────┘
             job-board sites
```

- **Core** — `job-seeker`, the only reachable server (SSL, `X-API-Key`).
  Owns the database and the AI queue. It never initiates a connection to
  any station.
- **Search terminal (browser host)** — any always-on desktop where the
  browser (with `agent-extension` loaded and agency sessions logged in) can
  run for hours. A station, not a server: the extension only makes outbound
  requests. Long uptime = scraping throughput. No code changes required.
- **AI station** — the GPU box running `llama-server` (localhost) plus
  **`ai-worker`, the one new program this design requires** (a small console
  project in this repository) and, from phase 5, the assistant browser. It
  reaches the core over the public SSL endpoint and nothing else; the core
  cannot see it back. `llama-server` stays localhost-bound **permanently**
  (2026-09-18): the assistant runs on this host, so no exposure decision
  remains (§3).
- **Assistant host (phase 5)** — the assistant browser runs on the AI
  station machine itself (2026-09-18; the planned separate "personal
  terminal" is dropped): outbound to the core with the Assistant key and to
  `llama-server` at localhost. Never both extensions in one browser.
- **Clients & roles.** Four clients reach the core: the search extension
  (header `X-Client: search` — shipped, informational only), the dashboard
  user, `ai-worker`, and the assistant (phase 5). Amended 2026-09-18 (D7,
  supersedes "roles are routing, not authorization" of 2026-09-15):
  **the key is the role.** Three per-client keys live in config/env —
  `Auth:ApiKeys:Dashboard`, `Auth:ApiKeys:Search`, `Auth:ApiKeys:Worker`
  (`Assistant` is added in phase 5; the single `Auth:ApiKey` mode is
  removed — clean cutover). The `Authorized` middleware in `Program.cs`
  matches each key (FixedTimeEquals) and enforces path rules: `search` →
  `/decision/*` (+ `/decision/scopes`), `worker` → `/ai/*`, `dashboard` →
  everything. `X-Client` remains an informational/logging header only — it
  grants nothing. Dashboard cookie login is unchanged; the login password
  is the Dashboard key. Production fail-fast applies only to a missing
  Dashboard key; missing Search/Worker keys log a warning. The
  reverse-proxy hardening recommendation stands (2026-09-15).

### 2.1 The ai-worker run — manual, no polling, no claim

Decided (2026-09-10; simplified 2026-09-11): `ai-worker` has **no polling
loop and no scheduler** — the user runs it manually whenever unranked jobs
should be processed. One run:

1. `GET /ai/next` (`X-Client: worker`) repeatedly, oldest first by `JobID`.
   The fetch is **read-only**: the response carries one `AiPending` job's
   text, the candidate's master resume text and the JobOption-derived
   `keywords` (2026-09-17), and changes no state. Phase 3 (2026-09-17)
   extends the payload with `settings` (`aiPassmark`), the per-job
   `options` as standard JSON, and the template-derived block `inventory`
   (§3). Since 2026-09-18 (D2) the payload also carries `fingerprint` =
   SHA-256 hex of the whitespace-normalized `Content` (the same Normalize
   as the content-change rule) — computed per request, nothing stored (no
   hash column). The worker stops when the core answers "empty".
2. For each job: run the local `llama-server` call(s) (§6), then
   `POST /ai/verdict` — the only write in the AI lane. Decided (2026-09-12,
   superseding the Pending-validation rule): the verdict is an **upsert** —
   it always updates the job. The core validates that the job exists and the
   payload is sane (AiScore 0–100, enum names, length caps), writes the
   verdict + extraction, and — only when the job is currently `AiPending` —
   applies the verdict gate: `AiScore ≥ AiPassmark` (an `AppSetting` key,
   default 60 — D3) promotes to `Attention`, otherwise `NotApprovedAI` (a
   purge candidate, §6). Verdicts arriving for out-of-queue jobs are logged
   informationally, not rejected. A re-POST after a lost HTTP response is a
   natural no-op, and re-running the worker overwrites old verdicts. The
   verdict echoes the `fingerprint`; the core recomputes it from the
   currently stored `Content` (2026-09-18, D2). A match applies the normal
   gate; a **mismatch** — the content changed between fetch and verdict —
   upserts the verdict columns and logs informationally with **no state
   transition**: the job stays `AiPending` and is re-judged on the next
   worker run. `Content == null` at verdict time counts as a mismatch.
   This closes the `/ai/next` ↔ `/ai/verdict` race.

- **No claim, no lease, no startup sweep** (2026-09-11, superseding the
  2026-09-10 sweep/claim design). A crash before the verdict simply leaves
  the job `AiPending` — the next run retries it naturally; nothing to reset.
  The rule stays **run one `ai-worker` at a time** (2026-09-12: a standing
  convention, not enforced in code): with upsert semantics a stray second
  instance can only waste compute, never corrupt state.
- The worker **never touches SQLite directly** — only the two endpoints.
- **Queue membership = `State = AiPending`** (2026-09-15; supersedes the
  planned `AiState` column): the regex gate enters the queue (§4.1), the
  verdict gate leaves it. Re-queue rules (amended 2026-09-18, D1): a
  settings change followed by `RunRevaluateProcess` re-queues floor-passing
  jobs in the **AI-domain, content-bearing states only** — `State IN
  ('Attention','AiPending','NotApprovedAI','AIError') AND Content IS NOT
  NULL` (supersedes the 2026-09-15 "every floor-passing job" wording; the
  old `State != 'Revaluation'` condition is absorbed). `Saved`,
  `Rejected`, `Applied` (and future `Duplicated`) are excluded —
  terminal/user-decided jobs keep Score/Log frozen as a historical record.
  Crash recovery returns stranded `Revaluation` rows to `Saved` with
  `Attempts = 0, Tries = NULL` — unified law: **any deliberate return to
  `Saved` for reprocessing resets `Attempts`** (the 2026-09-17
  floor-lowering resurrection included; without the reset the
  `Attempts >= 4` guard in `Q_FETCH_FIRST` would zombie-trap such jobs). A
  purged `NotApprovedRegex` job whose stored `Score` passes the new floor
  goes back to `Saved` for a natural re-scrape (2026-09-17 — purge nulls
  only `Html`/`Content`); a browser re-scrape re-queues only when the
  scraped text changed (whitespace-normalized compare against stored
  `Content`, 2026-09-17) — an unchanged re-visit leaves AI-judged states
  alone, while un-judged states (`Saved`, `AiPending`, `NotApprovedRegex`,
  `AIError`) always re-evaluate. A **per-job Revaluate button** (2026-09-18,
  D1.4) on job-detail force-re-evaluates one job: it ignores the phase-3
  `HumanEdited` guard (overwrites `Options` with the fresh regex context),
  keeps the explicit `Rejected`/`Applied` guard, and — from phase 3 —
  clears `AiOptions` and `ResumeText.proposal` (`live` text survives; no
  `AiTitle`); disabled when `Content == null`; a
  floor-passing job returns to `AiPending` (accepted); no param = the
  global process unchanged; no locking vs a concurrent global run (regex
  is deterministic; last write wins). A **dashboard re-queue button**
  (2026-09-18, D12) sets a single job back to `AiPending` from `AIError`
  or `NotApprovedAI` **only** — never from `Attention` (the per-job
  Revaluate button already covers its return to the queue), `Rejected`,
  `Applied`, or `Saved`; it is disabled/hidden when `Content == null`
  (after the 7-day `Q_CLEAN_NOT_APPROVED` those two re-queue sources
  can lack content — the SQL `WHERE` still covers the full rejected
  family including `NotApprovedRegex`; 2026-09-19 lock), and
  `FetchNextAiPending` defensively filters `Content IS NOT NULL` — a
  contentless job must never reach a verdict (consistent with D2's
  mismatch rule).
- **Rejected alternative: tunnels** (Tailscale, cloudflared, SSH reverse)
  would restore core→AI reachability and allow an in-core worker calling
  `llama-server` remotely. Rejected for Phase 0: extra infrastructure to keep
  alive, and the pull model needs none.

## 3. Runtime setup

- llama.cpp's `llama-server` exposes an OpenAI-compatible endpoint
  (`/v1/chat/completions`) on the AI station's localhost. The model never
  ships inside `job-seeker`; the core never talks to it at all. Decided
  (2026-09-12): localhost-only through phase 3; resolved 2026-09-18 —
  **permanent localhost**: the phase-5 assistant runs on the same host and
  shares the instance with the worker (`--parallel 2`, two independent
  clients; the total context `-c` splits across slots — provision ≥ ~32k:
  worker 16k + assistant ~8k). **No worker/assistant mutex** (2026-09-19):
  the user chooses when to run the worker; overlapping use is allowed;
  shared GPU latency is accepted. No interface binding, reverse proxy, or
  home-LAN trust assumption remains.
- `ai-worker` client: a plain `HttpClient` + JSON. No new package
  dependencies on the core.
- Suggested configuration (the worker's, not the core's `appsettings.json`;
  no `Enabled` kill-switch key — the worker runs manually, D17):

```json
"Llm": {
  "BaseUrl": "http://localhost:8082/v1",
  "Model": "Qwen3-30B-A3B-Q5_K_M",
  "Core": "https://core.example.com",
  "CoreApiKey": "<the core's worker key — Auth:ApiKeys:Worker, D7>",
  "Temperature": 0.2,
  "Seed": 42
}
```

- Concurrency: **one** in-flight request. `llama-server` processes requests
  sequentially; a single-consumer loop also keeps ordering debuggable.
- **Master resume source** (2026-09-15; resolves the round-3 storage
  question): one source — the existing template. The core renders
  `Views/resume.cshtml` with a fixed default `ResumeContext` (English, full
  skills profile — the model must see the candidate's whole genuine range),
  prunes the template superset server-side with HtmlAgilityPack (pattern:
  `JobEligibilityHelper.GetTextContent`), strips to text, and **caches the
  result in memory** (lazy first render; invalidated only by a process
  restart, so template changes take effect on the next deploy). The 16k
  token cap (§6) truncates from the top if needed. Served only through
  `GET /ai/next`; no second resume artifact to keep in sync. Phase 3
  (2026-09-17) extends the same endpoint with the call-2 inputs:
  `settings` (`aiPassmark` — drives the tailoring-threshold decision
  worker-side), the per-job `options` (`ResumeContext`) as **standard
  JSON** — `Job.Options` is itself stored as standard JSON via
  `ResumeContextTypeHandler` (`SqliteTypeHandlers.cs`); the simple-JSON
  format (`SimlpeSerialize`) is only the dashboard/client exchange
  format, which the worker never sees (wording corrected 2026-09-18) —
  and the template-derived block `inventory` (stable id = selector, type,
  `key-*` tags; editable text slots ship full template text, other items
  excerpt ≤ 120 chars, no item-count cap — D16 as amended 2026-09-19;
  cached in memory like the master resume).
- **Provider-agnostic by construction.** Because the worker's LLM client is a
  plain `HttpClient` speaking the OpenAI chat-completions protocol, any
  compatible endpoint works and switching is a config change only:
  - *Local:* `llama-server`, Ollama, LM Studio.
  - *Hosted:* OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini.
  - Hosted endpoints need an `"ApiKey"` entry in the `Llm` config — supplied
    via environment variables / secrets, never committed. **Policy
    (2026-09-10): local-only** — the lanes carry the resume (PII), so hosted
    endpoints are not used; they stay possible by config only if this
    policy ever changes (see the decision log).

## 4. Integration points, by priority

At a glance (details in the subsections below):

| # | Point | Lane | Impact | Risk |
|---|-------|------|--------|------|
| 4.1 | Semantic verdict / re-ranking | background | **High** — fixes the sharpest weakness: lexical scoring treats one ".NET" mention like a .NET-centric role; near-misses get a second chance | Low–medium — additive verdict columns + careful `Q_INDEX` v2 |
| 4.2 | Structured extraction | background | **High** — replaces guesswork heuristics (`EvaluateSalaryScore`); enables dashboard filters on salary/work-model reality | Low — additive columns only; `Q_INDEX` untouched |
| 4.3 | Resume tailoring delta | background (manual worker runs) | **Highest end value** — per-job customization: live selection plus accept-gated text in `ResumeText` ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3) | Medium — `Job.AiOptions` + `Job.ResumeText` + human text-accept |
| 4.4 | Cross-platform deduplication | background | **Medium** — one posting listed on two agencies stops being scored twice | Low |
| 4.5 | Dashboard digest stats | dashboard | **Low–medium** — closes the notification gap with no external service | Low — read-only over existing data |

### 4.1 Semantic verdict / re-ranking on queued (`AiPending`) jobs

Today `EvaluateEligibility` is exact regex matching: a job that mentions
".NET" once scores like a genuinely .NET-centric role. The LLM judges every
job the regex gate lets through — regex-approved (Score ≥ 100) and
near-misses (floor–99, floor default 70), all queued as `AiPending` (§6):

> Is this genuinely a senior full-stack role? Real seniority? Direct hire or
> staffing agency? Is relocation supported?

The prompt carries the job description **and the candidate's master resume
text** — a stable, job-independent base resume (decided 2026-09-12; source
decided 2026-09-15 — template-derived cache, §3) — so the verdict ranks
against the actual background. Output is a small JSON
verdict — `{ relevance: 0-100, seniority, verdict, reason }` — stored in
**separate additive columns** (e.g. `AiScore`, verdict, reason); the regex
`Score` is never overwritten, and `reason` is also appended to `job.Log`
(markdown-rendered on the dashboard).

Decided (2026-09-10): the verdict is **effective** — `AiScore` participates
in ranking. This deliberately overrides the earlier "don't touch `Q_INDEX`"
note: the ranking query must gain a careful v2 (enum-name literals, the
`Relocation` marker — see §4.2).

Decided (2026-09-11) — final score: `Score` (regex) and `AiScore` stay
separate columns; the blend is computed **at query time in `Q_INDEX` v2,
never stored**: `FinalScore = W_r * Score + W_a * AiScore`, initial
`W_r = 0.35`, `W_a = 0.65` (trial-and-error tunable), weights held in the
new `AppSetting` table (2026-09-18, D3) so they change without a rebuild.
Jobs without a verdict yet rank by their regex `Score`.

Amended (2026-09-12) — scale normalization. `AiScore` is fixed at **0–100**
(the model judges apply-worthiness from the master resume + JD + JobOption
weights). Regex `Score` is salary-inflated and unbounded, so the blend
normalizes first: `RegexNorm = min(Score, ScoreCap) / ScoreCap × 100` with
`ScoreCap` a new job-option setting (initial ~300, tunable);
`FinalScore = W_r * RegexNorm + W_a * AiScore` on a 0–100 scale;
verdict-less jobs rank by `RegexNorm`; the time-decay curve applies to
`FinalScore` post-blend (the `Analyze/JobRanking.cs` mirror of the decay
weights must be updated in the same change). Resolved (2026-09-15): there
is no blend-based promotion at all — the verdict gate (`AiScore ≥
AiPassmark`, default 60) decides `Attention` vs `NotApprovedAI`, and the
blend only orders lists, so weight changes can never invalidate a state.
Regex-vs-AI divergence statistics are dropped (scales differ — not a
requirement).

Amended (2026-09-15) — blend scope: `FinalScore` orders only verdict-bearing
states (`Attention`, `NotApprovedAI`); `AiPending` jobs rank by `RegexNorm`
alone. A re-queued job keeps its previous verdict columns visible but
unused — no destructive clear (re-queue rules in §2.1).

Amended (2026-09-18, D5): the same v2 blend keys `Q_CLEAN_ATTENTION` —
its top-100 Html-retention subquery orders by `FinalScore` (previously
raw `Score`); the blend expression is a shared C# SQL fragment (scorecap +
weights as SQL parameters from `AppSetting`) used by both `Q_INDEX` v2
and `Q_CLEAN_ATTENTION`.

Decided (2026-09-18, D11) — the concrete v2 dashboard layout. Category
numbers, display caps and page order are fixed: `Attention` (12 rows,
`FinalScore`) on top, then `AiPending` (6, `RegexNorm`), `NotApprovedAI`
(6, `FinalScore`), `Applied`/`Rejected` (3 each, unchanged),
`NotApprovedRegex` (6, `RegexNorm`), `AIError` (3, `RegexNorm`),
`Saved`/`Revaluation` (1, unchanged); within `Attention`, verdict-less
(manually promoted) jobs COALESCE to `RegexNorm`. Rationale: actionable
rows (AI-approved, queue) on top; bulk/informational bands below. Full
table in [`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md) (`JobBusiness`).

Decided (2026-09-15) — sequential state machine (supersedes the 2026-09-11
`Job.AiState` column; queue membership is `State = AiPending`). The regex
gate writes its own domain: below the configurable floor (default 70) →
`NotApprovedRegex` with `Html`/`Content` purged; at or above the floor →
`AiPending` (both the floor–99 near-miss band and 100+). The verdict gate
writes the AI domain: `AiScore ≥ AiPassmark` (default 60) → `Attention`,
otherwise `NotApprovedAI`; an error verdict → `AIError`, bypassing the
passmark gate (2026-09-17). Enum order: `Saved, Revaluation,
NotApprovedRegex, AiPending, NotApprovedAI, AIError, Attention, Rejected,
Applied`;
the order-dependent `user_changes` guard (`State > Attention`) becomes an
explicit `is Rejected or Applied` check. Browser follow-up commands
(`JobPage`/`StepstonePageJob` save-button clicks) re-key from `Attention`
to the regex-approval result, so browser behavior is unchanged. **No
bypass** (2026-09-15; amended 2026-09-18, D8): `Attention` is reachable
only via a verdict — no AI-off fallback — with one emergency exception: a
dashboard button (`POST /job/promote?jobid=`) promotes a single job from
`AiPending`, `NotApprovedAI` or `AIError` to `Attention` (never from
`Rejected`/`Applied`, or future `Duplicated`). No synthetic `AiScore` is
written — within `Attention` the promoted job ranks by `RegexNorm`
(COALESCE-style in `Q_INDEX` v2); the action appends `Manually promoted
(emergency) — <date>` to `job.Log` and bumps `ModifiedOn`; the job leaves
the queue and the worker will not see it again until a changed re-scrape
or a revaluation re-queues it (accepted). Otherwise the top list stays
empty until the worker runs.
Implementation starts from a **fresh database** (old jobs expired) — no row
migration, no backfill. Purge candidacy is `NotApprovedAI` and `AIError`
(§6).

Ranking feedback (2026-09-19, [`GLOSSARY.md`](GLOSSARY.md)): there is **no
override log**. The F1 memory-injection slots are labeled blocks
snapshotted at **worker run start** (D6 determinism / prefix cache):
**confirmed** `Scope = ranking` into call 1, **confirmed** `Scope = resume`
into call 2. Unconfirmed rows and `job.Log` audit lines are not injected.
`POST /ai/verdict` has no `memory[]` field; the worker does not write
memory. `memorycap` limits how many confirmed rows are sent, not how many
are stored. See [`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §4.

### 4.2 Structured extraction

Replace heuristics like `EvaluateSalaryScore`, which guesses "amount > 35000
⇒ annual". With JSON-schema-constrained generation the model always emits
parseable JSON:

```json
{
  "salary_min": 60000, "salary_max": 80000, "currency": "EUR", "period": "year",
  "seniority": "senior", "work_model": "hybrid", "contract": "b2b",
  "experience_years": 5, "skills": [".NET", "C#", "Azure"]
}
```

Stored as **new additive columns on the same `Job` table** (1:1 — no new
table): `AiSalaryMin`, `AiSalaryMax`, `AiCurrency`, `AiPeriod`,
`AiSeniority`, `AiWorkModel`, `AiContract`, `AiExperienceYears`, and
`AiSkills` as JSON via the existing type-handler pattern; enum-valued
columns store the enum *name* as text (repo rule). This enables dashboard
filters by work model / salary reality instead of approximate score.

Decided (2026-09-11): the extraction is produced by the **same worker pass
as the verdict** — one JSON-schema-constrained call returns both (§6). The
former phase 2 is thereby absorbed into phase 1; phases 3–5 keep their
numbers (§7).

> **`JobBusiness.Q_INDEX` is fragile — edit only as a deliberate v2.** The
> ranking SQL silently depends on enum-name string literals and the
> `Relocation` log marker (see "Things that bite" in
> [`AGENTS.md`](../AGENTS.md)). Phase 1's ranking integration (§4.1) is the
> one sanctioned reason to touch it; additive columns remain safe.

### 4.3 Resume tailoring (decided end-to-end flow)

The strongest free-text use case — see the companion doc
[`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md). The decided flow:

1. The browser reaches the job page; the server scrapes the job description
   and scores it. The existing regex path already builds the initial
   `ResumeContext` (`Job.Options`) from the JD's keywords.
2. The LLM refines that context into a reviewable selection delta stored in
   `Job.AiOptions`, and may propose title / summary / bullet wording into
   `Job.ResumeText.proposal` — produced during manual `ai-worker` runs
   (§2.1), before a human ever reviews the job. Text proposals never
   render until accept/edit; selection is live immediately.
3. The user opens `/job/resume?jobid=...` (or downloads the rendered HTML via
   `/job/resume64`) and **prints from the browser** (Brave). The view already
   carries `@media print` CSS, so it is print-ready as served. No server-side
   PDF conversion and no new `print` browser command are planned.

### 4.4 Cross-platform deduplication

A cheap prefilter (same normalized title + company across agencies) produces
candidate pairs; the LLM judges whether two postings are the same job.
Decided (2026-09-10): the result is **advisory** — a dashboard warning the
user can clear; on confirmation the second posting moves to a new
`Duplicated` state (excluded from ranking). Closes the known gap that one
posting listed on both Indeed and LinkedIn gets scored twice.

### 4.5 Daily digest / dashboard stats

Decided (2026-09-10): the digest is **dashboard-only statistics** — no
external notification channel. The dashboard reflects counts of new jobs,
good jobs (found by the regex gate) and strong jobs (found by the AI
verdict), matching the "leave the system running" usage pattern.

## 5. Where NOT to use the LLM

| Place | Why |
|-------|-----|
| `Command[]` / `TrendsCheckpoint` | Must stay deterministic and instant. An LLM error means a lost/confused browser tab, and debugging nondeterministic control flow is misery. |
| Page detection regexes (`LoginPage`, `SearchPage`, ...) | Fast and adequate. The model only adds latency and nondeterminism where none is needed. |
| `LanguageIsMatch` | The dictionary lookup is instant and free. Leave it alone. |

## 6. Practical notes

- **Speed.** `Qwen3-30B-A3B` is MoE (~3B active parameters): expect tens of
  tokens/sec on consumer hardware. A verdict prompt is ~2–3k tokens in,
  ~100 tokens out — a few seconds per job. Overnight batches of hundreds of
  jobs are comfortable.
- **Prompt caching.** `llama-server` caches the shared prompt prefix. Put the
  fixed rubric (scoring criteria + candidate profile) in the system prompt so
  only the job description differs per call. Prefix-cache scope (2026-09-18):
  call 1 and call 2 share **no** cached prefix — their rubrics diverge at
  the first token — but the cache pays off **across jobs of the same call
  type**: F1's stable block order keeps the fixed parts (rubric + master
  resume / rubric + inventory) at the front and only the JD varies at the
  tail.
- **Structured output.** Use `response_format` (JSON schema / GBNF grammar)
  so verdicts and extractions always parse. Never regex-scrape model output.
- **Determinism (2026-09-18, D6).** Every model request carries
  `Temperature` (default 0.2) and a fixed `Seed` (`llama-server` supports
  both). Re-runs must be stable so `AiScore` cannot flip around
  `aiPassmark` without an input change.
- **Two-stage purge — stage 2 human-confirmed (decided 2026-09-10; amended
  2026-09-11, 2026-09-12, 2026-09-15).** Stage 1: jobs scoring **below the
  configurable near-miss floor** purge immediately — the floor is an
  `AppSetting` key (2026-09-18, D3) shared with AI-queue entry, and this
  is a **deliberate behavior change**: today's code purges everything below
  `MinEligibilityScore = 100`, so retaining the floor–99 band (with its DB
  growth) is part of the phase-1 deliverables (§7). Amended (2026-09-15):
  the floor's default is **70** and it is now a state boundary — below it
  the regex gate writes `NotApprovedRegex`, at or above it `AiPending`
  (§4.1); the earlier "the floor never changes `JobState`" guarantee no
  longer applies. Stage 2: the AI queue is **`State = AiPending`**;
  `Html`/`Content` is retained until the verdict lands. A weak verdict
  moves the job to `NotApprovedAI` as a **purge candidate**; actual deletion
  is a dashboard action (single or bulk, pre-selected by threshold), and
  the existing old-age cleanup also covers `NotApprovedRegex` and
  `NotApprovedAI`. `GET /ai/next` carries the job text (`Job.Content`), the
  content `fingerprint` (D2, §2.1) and, per the ranking decision (§4.1),
  the master resume text.
- **Poison jobs (decided 2026-09-15; amended 2026-09-17 — target state).**
  A model/parse failure is retried twice within the same worker run;
  persistent failure posts an **error verdict** moving the job to
  `AIError` with the error as its reason. Without it, oldest-first would
  spin the worker on the same broken job forever. The dashboard re-queue
  button (job → `AiPending`) retries later (source states `AIError`/
  `NotApprovedAI` only + the `Content == null` guard — D12, §2.1).
- **Two model calls per worker pass (decided 2026-09-11; phase-3 contract
  2026-09-17, amended 2026-09-19).** Call 1 (always): verdict + extraction as one
  JSON-schema-constrained response — input: system rubric + candidate
  resume text + JD; output: `{ relevance, seniority, verdict, reason,
  salary_min/max, currency, period, work_model, contract,
  experience_years, skills[] }`. Call 2 (conditional): the resume-tailoring
  delta — runs only when the same verdict promotes the job to `Attention`
  (relevance ≥ `AiPassmark`, shipped in `/ai/next` `settings`); input:
  rubric + block inventory + current context + JD (all from `/ai/next`;
  the fixed candidate profile sits in the call-2 system prompt — its own
  config key `Llm:RubricTailor`, D14, draft in
  [`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md)); inventory: full template
  text on editable slots (title / summary / job-description bullets),
  ≤ 120 char excerpt on other items, no item-count cap (D16 as amended
  2026-09-19); the 16k cap applies **per call**, JD tail-truncated — do
  not raise the cap speculatively. The worker posts the raw payload
  inside `POST /ai/verdict`; the **core validates independently**:
  selection (`keys` ⊆ `MainKeys`; selectors ⊆ inventory — a whitelist,
  not syntax-only; length ∈ {1,2}; caps ≤ 8 keys / ≤ 30 selectors)
  applies onto `Options` and stores the complete context in `AiOptions`;
  a valid `texts` map writes `ResumeText.proposal` (title one line ≤ 80
  chars; no HTML). The raw payload is appended to `job.Log`. An invalid
  texts map must not drop a valid selection delta. An invalid selection after the worker's two call-2 retries is
  dropped — the verdict applies alone; the core likewise drops any delta
  arriving with a non-promoting or `Error` verdict (`AIError` carries no
  delta — call 2 never runs after persistent call-1 failure). The two
  calls fail independently: a call-2 connection failure aborts the run
  and writes nothing; a model-output failure retries twice, then the
  verdict posts alone. Rejected jobs never pay for tailoring.
- **Context length.** Configurable cap, **16k tokens to start** (32k
  acceptable), tuned by trial and error; budget: rubric (~1k) + master
  resume in full + JD remainder, an over-long JD truncated at its **tail**
  — requirements live early (clarified 2026-09-17). The ranking prompt also
  carries the resume text (§4.1) and, from phase 5, snapshots of confirmed
  ranking memory (call 1) and resume memory (call 2) (F1). Worker calls
  time out at 120 s;
  connection failures abort the whole run and write nothing (error classes
  per the 2026-09-17 decision-log entry).
- **Core-error protocol (2026-09-18, D13).** `GET /ai/next`: any network
  error or non-200 aborts the whole run. `POST /ai/verdict`: 404 (job
  deleted between fetch and verdict) → log + continue with the next job;
  400 (validation rejection = a worker bug) → abort the run loudly; 5xx
  or network error → abort the run. Loop-safety: without these rules the
  worker could spin endlessly on the same oldest `AiPending` job.

## 7. Phasing

| Phase | Deliverable | Risk |
|-------|-------------|------|
| 0 | Topology (§2) — documentation only | Decided 2026-09-10: no standalone deliverable |
| 1 | Verdict + extraction + ranking: `ai-worker` + `GET /ai/next` & `POST /ai/verdict` (built here) + additive verdict/extraction columns + `Q_INDEX` v2 (dashboard category layout per D11) + the sequential state machine (`NotApprovedRegex`/`AiPending`/`NotApprovedAI`; queue = `AiPending`, no `AiState` column) + near-miss retention (floor default 70) + new `AppSetting` table for the five settings (floor=70, aipassmark=60, scorecap=300, w_regex=0.35, w_ai=0.65 — D3) + per-client API keys (D7) + verdict fingerprint (D2) + emergency promote button (D8) + fresh DB (no migration) | Worker infra absorbed into phase 1; the ranking edit is the delicate part |
| ~~2~~ | ~~Structured extraction columns~~ — **absorbed into phase 1** (2026-09-11: one worker pass produces verdict + extraction); dashboard filters deferred to phase 6 (2026-09-17) | Additive schema only |
| 3 | Resume tailoring (`Job.AiOptions`, `Job.ResumeText`) — two-layer, 2026-09-19 (decision log) | Selection review = user duty on job-detail; text proposals stay accept-gated; no `AiTitle` |
| 4 | Cross-platform dedup + daily digest | Read-only over existing data |
| 5 | Apply assistant — browser on the AI station ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)): generic DOM Fill, memory UI in the assistant, dual Applied marks, page modes | New extension + `/assistant/*`; human presses every submit; per-site adapters later |
| 5.5 | Compose — accept-gated long-form (cover letter / screening essays) then fill (policy 2026-09-19; not built with phase 5) | Same assistant; still no submit tool |
| 6 | Dashboard side-work (2026-09-17): filters over the extraction columns (design + implementation), bulk purge for `NotApprovedAI`/`AIError`, digest-count redefinition + `AiPending` counter for data-driven floor tuning (2026-09-18, D9). Memory UI is **phase 5** (2026-09-19), not this phase. | Read-only over existing data; no browser/worker changes |

Best value-to-risk is still **phase 1** (fixes lexical regex scoring;
carries the worker, the structured extraction and the `Q_INDEX` v2 edit).
The highest end value sits in **phase 3**: live selection plus
accept-gated text on a closed slot set is the line between tailoring and
fabrication ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3).

## 8. Decision log

Moved to [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) (2026-09-15) — the log
outgrew this document's file-size budget. Entries are immutable history:
supersessions are recorded as annotations or later entries, never edits.
Section references (§N) inside entries point back into this document.
