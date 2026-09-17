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
> `AiState`-column design (§4.1 and the decision log).

Related: [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) (phase 3 detail),
[`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) (phase 5 detail).
Decisions: [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md). Implementation
pointers: [`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md).

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

Four stations, three of them outbound-only (the fourth is planned):

```
                ┌────────────────────────────────┐
                │  core: job-seeker (.NET)       │  public, SSL (reverse proxy),
                │  SQLite + AI queue + API       │  X-API-Key + X-Client role
                └─▲──────────────▲─────────────▲─┘
   POST /decision/take             │            │  GET /ai/next · POST /ai/verdict
   GET  /decision/scopes           │            │  POST /assistant/* (planned —
   (X-Client: search)              │            │  phase 5, X-Client: assistant)
  ┌────────────────────────────────┴─┐        ┌─┴──────────────────────┐      ┌─────────────────────────────┐
  │ search terminal (browser host)   │        │ AI station             │ LAN  │ personal terminal (planned) │
  │ long-running desktop; logged-in  │        │ GPU box behind NAT —   │◄─────│ assistant-extension: apply- │
  │ browser + agent-extension;       │        │ NOT reachable from the │ LLM  │ form filling + chat;        │
  │ outbound only                    │        │ core; ai-worker +      │only  │ outbound only               │
  └────────────────┬─────────────────┘        │ llama-server (localhost│      └─────────────────────────────┘
                   │ HTTPS                    └────────────────────────┘
                   ▼
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
  project in this repository). It reaches the core over the public SSL
  endpoint and nothing else; the core cannot see it back. `llama-server`
  stays localhost-bound through phase 3 (2026-09-12); any LAN exposure for
  the phase-5 assistant is a phase-5 decision (§3).
- **Personal terminal (phase 5 — planned)** — the assistant's host: the
  user's own desktop, running the planned `assistant-extension`
  ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)). Outbound-only to the
  core (`X-Client: assistant`) and, over the LAN, to the AI station's
  `llama-server`. Never the search extension and the assistant in one
  browser — the search extension matches `*://*/*` and would fight over the
  apply tabs.
- **Clients & roles.** Four clients reach the core: the search extension
  (`X-Client: search` — shipped in phase 1, decided 2026-09-11: a one-line
  extension change, single deployment), the dashboard user (existing
  login/API-key — no role header), `ai-worker` (`X-Client: worker`,
  restricted to `/ai/*`), and the assistant (`X-Client: assistant`). An
  absent header is treated as legacy search. **Roles are routing, not
  authorization** (2026-09-15): `X-Client` is a claim anyone holding the
  ApiKey can send — the security boundary is the shared `X-API-Key` (plus
  the dashboard cookie); the worker role's `/ai/*` restriction is a
  behavioral filter, not a permission wall.

### 2.1 The ai-worker run — manual, no polling, no claim

Decided (2026-09-10; simplified 2026-09-11): `ai-worker` has **no polling
loop and no scheduler** — the user runs it manually whenever unranked jobs
should be processed. One run:

1. `GET /ai/next` (`X-Client: worker`) repeatedly, oldest first by `JobID`.
   The fetch is **read-only**: the response carries one `AiPending` job's
   text plus the candidate's master resume text, and changes no state. The
   worker stops when the core answers "empty".
2. For each job: run the local `llama-server` call(s) (§6), then
   `POST /ai/verdict` — the only write in the AI lane. Decided (2026-09-12,
   superseding the Pending-validation rule): the verdict is an **upsert** —
   it always updates the job. The core validates that the job exists and the
   payload is sane (AiScore 0–100, enum names, length caps), writes the
   verdict + extraction, and — only when the job is currently `AiPending` —
   applies the verdict gate: `AiScore ≥ AiPassmark` (job-option setting,
   default 60) promotes to `Attention`, otherwise `NotApprovedAI` (a purge
   candidate, §6). Verdicts arriving for out-of-queue jobs are logged
   informationally, not rejected. A re-POST after a lost HTTP response is a
   natural no-op, and re-running the worker overwrites old verdicts.

- **No claim, no lease, no startup sweep** (2026-09-11, superseding the
  2026-09-10 sweep/claim design). A crash before the verdict simply leaves
  the job `AiPending` — the next run retries it naturally; nothing to reset.
  The rule stays **run one `ai-worker` at a time** (2026-09-12: a standing
  convention, not enforced in code): with upsert semantics a stray second
  instance can only waste compute, never corrupt state.
- The worker **never touches SQLite directly** — only the two endpoints.
- **Queue membership = `State = AiPending`** (2026-09-15; supersedes the
  planned `AiState` column): the regex gate enters the queue (§4.1), the
  verdict gate leaves it. Re-queue rules: a settings change followed by
  `RunRevaluateProcess` re-queues **every** floor-passing job (including
  `Attention`/`NotApprovedAI`); a purged `NotApprovedRegex` job whose
  stored `Score` passes the new floor goes back to `Saved` for a natural
  re-scrape (2026-09-17 — purge nulls only `Html`/`Content`); a browser
  re-scrape re-queues only when the scraped text changed (whitespace-
  normalized compare against stored `Content`, 2026-09-17) — an unchanged
  re-visit leaves AI-judged states alone, while un-judged states (`Saved`,
  `AiPending`, `NotApprovedRegex`, `AIError`) always re-evaluate.
- **Rejected alternative: tunnels** (Tailscale, cloudflared, SSH reverse)
  would restore core→AI reachability and allow an in-core worker calling
  `llama-server` remotely. Rejected for Phase 0: extra infrastructure to keep
  alive, and the pull model needs none.

## 3. Runtime setup

- llama.cpp's `llama-server` exposes an OpenAI-compatible endpoint
  (`/v1/chat/completions`) on the AI station's localhost. The model never
  ships inside `job-seeker`; the core never talks to it at all — `ai-worker`
  is its only client. Decided (2026-09-12): localhost-only through phase 3;
  any LAN exposure for the phase-5 assistant (interface binding or a local
  reverse proxy, and the home-LAN trust assumption it implies) is decided in
  phase 5 — this resolves the earlier §2-diagram /
  [`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) contradiction.
- `ai-worker` client: a plain `HttpClient` + JSON. No new package
  dependencies on the core.
- Suggested configuration (the worker's, not the core's `appsettings.json`):

```json
"Llm": {
  "BaseUrl": "http://localhost:8082/v1",
  "Model": "Qwen3-30B-A3B-Q5_K_M",
  "Core": "https://core.example.com",
  "Enabled": true
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
  `GET /ai/next`; no second resume artifact to keep in sync.
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
| 4.3 | Resume tailoring delta | background (manual worker runs) | **Highest end value** — per-job customization, selection-only except the guarded title exception ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3) | Medium — new `Job.AiOptions` column + human review gate |
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
`W_r = 0.35`, `W_a = 0.65` (trial-and-error tunable), weights held in
job-option settings so they change without a rebuild. Jobs without a
verdict yet rank by their regex `Score`.

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
bypass:** `Attention` is reachable only via a verdict — no AI-off fallback,
no manual promote; the top list stays empty until the worker runs.
Implementation starts from a **fresh database** (old jobs expired) — no row
migration, no backfill. Purge candidacy is `NotApprovedAI` and `AIError`
(§6).

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
2. The LLM refines that context into a reviewable delta stored in
   `Job.AiOptions` — produced during manual `ai-worker` runs (§2.1), before
   a human ever reviews the job.
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
  only the job description differs per call.
- **Structured output.** Use `response_format` (JSON schema / GBNF grammar)
  so verdicts and extractions always parse. Never regex-scrape model output.
- **Two-stage purge — stage 2 human-confirmed (decided 2026-09-10; amended
  2026-09-11, 2026-09-12, 2026-09-15).** Stage 1: jobs scoring **below the
  configurable near-miss floor** purge immediately — the floor is a
  job-option setting shared with AI-queue entry, and this is a **deliberate
  behavior change**: today's code purges everything below
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
  `NotApprovedAI`. `GET /ai/next` carries the job text (`Job.Content`) and,
  per the ranking decision (§4.1), the master resume text.
- **Poison jobs (decided 2026-09-15; amended 2026-09-17 — target state).**
  A model/parse failure is retried twice within the same worker run;
  persistent failure posts an **error verdict** moving the job to
  `AIError` with the error as its reason. Without it, oldest-first would
  spin the worker on the same broken job forever. The dashboard re-queue
  button (job → `AiPending`) retries later.
- **Two model calls per worker pass (decided 2026-09-11).** Call 1
  (always): verdict + extraction as one JSON-schema-constrained response —
  input: system rubric + candidate resume text + JD; output:
  `{ relevance, seniority, verdict, reason, salary_min/max, currency,
  period, work_model, contract, experience_years, skills[] }`. Call 2
  (conditional, relevance ≥ threshold only): the resume-tailoring delta —
  input: JD + block inventory + current ResumeContext + profile; activated
  in phase 3. Both results return to the core in one idempotent
  `POST /ai/verdict`. Rejected jobs never pay for tailoring; smaller
  schemas parse more reliably; the two calls retry independently.
- **Context length.** Configurable cap, **16k tokens to start** (32k
  acceptable), tuned by trial and error; budget: rubric (~1k) + master
  resume in full + JD remainder, an over-long JD truncated at its **tail**
  — requirements live early (clarified 2026-09-17). The ranking prompt also
  carries the resume text (§4.1). Worker calls time out at 120 s;
  connection failures abort the whole run and write nothing (error classes
  per the 2026-09-17 decision-log entry).

## 7. Phasing

| Phase | Deliverable | Risk |
|-------|-------------|------|
| 0 | Topology (§2) — documentation only | Decided 2026-09-10: no standalone deliverable |
| 1 | Verdict + extraction + ranking: `ai-worker` + `GET /ai/next` & `POST /ai/verdict` (built here) + additive verdict/extraction columns + `Q_INDEX` v2 + the sequential state machine (`NotApprovedRegex`/`AiPending`/`NotApprovedAI`; queue = `AiPending`, no `AiState` column) + near-miss retention (floor default 70) + new job-option settings (floor, aipassmark=60, scorecap=300, w_regex=0.35, w_ai=0.65) + fresh DB (no migration) | Worker infra absorbed into phase 1; the ranking edit is the delicate part |
| ~~2~~ | ~~Structured extraction columns~~ — **absorbed into phase 1** (2026-09-11: one worker pass produces verdict + extraction); dashboard filters deferred to phase 6 (2026-09-17) | Additive schema only |
| 3 | Resume tailoring delta (`Job.AiOptions`) | Human review gate before `Applied` |
| 4 | Cross-platform dedup + daily digest | Read-only over existing data |
| 5 | Apply assistant on a personal terminal ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)) | New extension + `/assistant/*` endpoints; the human presses every submit |
| 6 | Dashboard side-work (2026-09-17): filters over the extraction columns (design + implementation), bulk purge for `NotApprovedAI`/`AIError`, digest-count redefinition | Read-only over existing data; no browser/worker changes |

Best value-to-risk is still **phase 1** (fixes lexical regex scoring;
carries the worker, the structured extraction and the `Q_INDEX` v2 edit).
The highest end value sits in **phase 3**: selection-only tailoring is the
line between tailoring and fabrication
([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3).

## 8. Decision log

Moved to [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) (2026-09-15) — the log
outgrew this document's file-size budget. Entries are immutable history:
supersessions are recorded as annotations or later entries, never edits.
Section references (§N) inside entries point back into this document.
