# AI integration

> **Status: design proposal + recorded decisions — not implemented.** This
> document consolidates everything about using AI in this system: the decided
> deployment topology (§2), the agreed design for adding an LLM (local-only:
> llama.cpp `llama-server` with a GGUF model such as
> `Qwen3-30B-A3B-Q5_K_M`), the integration points by priority, and the
> decisions taken so far (§8). No AI code exists yet; nothing here describes
> current behavior.

Related: [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) (phase 3 detail),
[`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) (phase 5 detail).

## 1. The golden rule: the LLM never sits in the browser loop

`POST /decision/take` is the path the extension blocks on, and the whole
scraping loop is paced by its response time. LLM inference takes seconds;
the loop must stay deterministic and millisecond-fast. So the system gets
two lanes:

```
browser loop (fast, deterministic)          background AI lane (slow, smart)
──────────────────────────────────          ──────────────────────────────────
regex scoring (unchanged)              →    queue on the core → ai-worker on
Job saved with State = Attention       →   the AI station → verdict, extrac-
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
human applies manually — see §8), and `ai-worker` runs manually (§2.1), so
deltas are produced before a human ever reviews a job. Nothing in the
browser loop ever waits on the model.

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
  absent header is treated as legacy search.

### 2.1 The ai-worker run — manual, no polling, no claim

Decided (2026-09-10; simplified 2026-09-11): `ai-worker` has **no polling
loop and no scheduler** — the user runs it manually whenever unranked jobs
should be processed. One run:

1. `GET /ai/next` (`X-Client: worker`) repeatedly, oldest first. The fetch
   is **read-only**: the response carries one `Pending` job's text plus the
   candidate's master resume text, and changes no state. The worker stops when
   the core answers "empty".
2. For each job: run the local `llama-server` call(s) (§6), then
   `POST /ai/verdict` — the only write in the AI lane. Decided (2026-09-12,
   superseding the Pending-validation rule): the verdict is an **upsert** —
   it always updates the job. The core validates that the job exists and the
   payload is sane (AiScore 0–100, enum names, length caps), writes the
   verdict + extraction, sets `AiState = Processed`, and marks purge
   candidates (§6); verdicts arriving for out-of-queue jobs are logged
   informationally, not rejected. A re-POST after a lost HTTP response is a
   natural no-op, and re-running the worker overwrites old verdicts.

- **No claim, no lease, no startup sweep** (2026-09-11, superseding the
  2026-09-10 sweep/claim design). A crash before the verdict simply leaves
  the job `Pending` — the next run retries it naturally; nothing to reset.
  The rule stays **run one `ai-worker` at a time** (2026-09-12: a standing
  convention, not enforced in code): with upsert semantics a stray second
  instance can only waste compute, never corrupt state.
- The worker **never touches SQLite directly** — only the two endpoints.
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
- **Provider-agnostic by construction.** Because the worker's LLM client is a
  plain `HttpClient` speaking the OpenAI chat-completions protocol, any
  compatible endpoint works and switching is a config change only:
  - *Local:* `llama-server`, Ollama, LM Studio.
  - *Hosted:* OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini.
  - Hosted endpoints need an `"ApiKey"` entry in the `Llm` config — supplied
    via environment variables / secrets, never committed. **Policy
    (2026-09-10): local-only** — the lanes carry the resume (PII), so hosted
    endpoints are not used; they stay possible by config only if this
    policy ever changes (§8).

## 4. Integration points, by priority

At a glance (details in the subsections below):

| # | Point | Lane | Impact | Risk |
|---|-------|------|--------|------|
| 4.1 | Semantic verdict / re-ranking | background | **High** — fixes the sharpest weakness: lexical scoring treats one ".NET" mention like a .NET-centric role; near-misses get a second chance | Low–medium — additive verdict columns + careful `Q_INDEX` v2 |
| 4.2 | Structured extraction | background | **High** — replaces guesswork heuristics (`EvaluateSalaryScore`); enables dashboard filters on salary/work-model reality | Low — additive columns only; `Q_INDEX` untouched |
| 4.3 | Resume tailoring delta | background (manual worker runs) | **Highest end value** — per-job customization, selection-only except the guarded title exception ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3) | Medium — new `Job.AiOptions` column + human review gate |
| 4.4 | Cross-platform deduplication | background | **Medium** — one posting listed on two agencies stops being scored twice | Low |
| 4.5 | Dashboard digest stats | dashboard | **Low–medium** — closes the notification gap with no external service | Low — read-only over existing data |

### 4.1 Semantic verdict / re-ranking on `Attention` + near-miss jobs

Today `EvaluateEligibility` is exact regex matching: a job that mentions
".NET" once scores like a genuinely .NET-centric role. The LLM judges two
bands: jobs that passed the regex gate (`State = Attention`) and near-misses
(score 50–99) given a second chance (§6):

> Is this genuinely a senior full-stack role? Real seniority? Direct hire or
> staffing agency? Is relocation supported?

The prompt carries the job description **and the candidate's master resume
text** — a stable, job-independent base resume (decided 2026-09-12; storage
source open, round 3) — so the verdict ranks against the actual background. Output is a small JSON
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
weights must be updated in the same change). Open (round 3): whether a
near-miss crossing the second-chance threshold gets a dynamic top category
in `Q_INDEX` v2 or a `JobState` change. Regex-vs-AI divergence statistics
are dropped (scales differ — not a requirement).

Decided (2026-09-11) — AI status encoding: a new additive `Job.AiState`
column (enum name as text). `Pending` is set by the core when a job enters
the queue (Attention + near-miss 50–99); `Processed` is set by
`/ai/verdict` after validating the job is `Pending`; NULL = outside the
queue — the default for all existing rows, honoring the no-backfill
decision. Purge candidacy derives from `AiState = 'Processed'` + `AiScore`
below threshold. *(The "non-Pending verdicts rejected" clause was
superseded 2026-09-12 by upsert semantics — §2.1.)*

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
  2026-09-11, 2026-09-12).** Stage 1: jobs scoring **below the configurable
  near-miss floor** purge immediately — the floor (e.g. 50 or 70) is a
  job-option setting shared with AI-queue entry, and this is a **deliberate
  behavior change**: today's code purges everything below
  `MinEligibilityScore = 100`, so retaining the floor–99 band (with its DB
  growth) is part of the phase-1 deliverables (§7). The Attention pass-mark
  stays fixed at 100 — the floor never changes `JobState`. Stage 2: the AI
  queue covers **Attention + near-miss (floor–99)**; their `Html`/`Content`
  is retained until the verdict lands. A weak verdict now only marks the job
  a **purge candidate**; actual deletion is a dashboard action (single or
  bulk, pre-selected by threshold). `GET /ai/next` carries the job text and,
  per the ranking decision (§4.1), the master resume text.
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
  acceptable), tuned by trial and error; truncate from the top, requirements
  live early. The ranking prompt also carries the resume text (§4.1).

## 7. Phasing

| Phase | Deliverable | Risk |
|-------|-------------|------|
| 0 | Topology (§2) — documentation only | Decided 2026-09-10: no standalone deliverable |
| 1 | Verdict + extraction + ranking: `ai-worker` + `GET /ai/next` & `POST /ai/verdict` (built here) + additive verdict/extraction columns + `Q_INDEX` v2 + near-miss retention change (purge reads the floor setting; written together with `AiState = Pending` — §6) | Worker infra absorbed into phase 1; the ranking edit is the delicate part |
| ~~2~~ | ~~Structured extraction columns~~ — **absorbed into phase 1** (2026-09-11: one worker pass produces verdict + extraction); dashboard filters may still land separately | Additive schema only |
| 3 | Resume tailoring delta (`Job.AiOptions`) | Human review gate before `Applied` |
| 4 | Cross-platform dedup + daily digest | Read-only over existing data |
| 5 | Apply assistant on a personal terminal ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)) | New extension + `/assistant/*` endpoints; the human presses every submit |

Best ratio of value to risk is still **phase 1**: it corrects the most
precise weakness of the current pipeline — lexical regex scoring — and now
also carries the worker infrastructure, the structured extraction, and the
`Q_INDEX` v2 edit. The highest end value sits in **phase 3**: the delta
pattern means the model can only *select* among pre-written blocks (now
including the summary and headline variants, plus the guarded title-only
free-text exception — see [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md)
§3), which on a factual document is the line between tailoring and
fabrication.

## 8. Decision log

| Date | Decision |
|------|----------|
| 2026-09 | **CloudConvert retired.** The HTML→PDF conversion path in `wwwroot/scripts/resume-pdf.js` is no longer used and its `API_KEY` was removed from the repo. Final delivery of the resume is a manual print from the browser (Brave) against `/job/resume`, which is print-ready via its `@media print` rules. The file remains loaded by `Views/layout.cshtml` as dead code pending cleanup. |
| 2026-09 | **No `print` PageAction.** The extension command vocabulary (`go/open/fill/click/recheck/close/wait/reload`) stays as is; turning the resume into PDF/paper is a human step, not a browser command. |
| 2026-09 | **Resume-from-JD at the job stage is the target flow** (§4.3): regex `ResumeContext` first, LLM delta on top, human review, then print from `/job/resume`. |
| 2026-09 | **Apply-stage exception to the golden rule** (§1): a delay of a few seconds is acceptable when a single job page is in flight and a human reviews the output before printing. |
| 2026-09 | **Provider-agnostic client** (§3): plain `HttpClient` + OpenAI-compatible endpoint; local (`llama-server`, Ollama, LM Studio) or hosted (OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini) chosen by config only. |
| 2026-09 | **Phase 0 topology: three stations, pull model** (§2). Core (`job-seeker`, public SSL, `X-API-Key`) is the only reachable server. The search agent's host is a long-running, outbound-only browser desktop — a station, not a server, no code changes. The AI station (GPU box) is unreachable from the core, so the AI lane runs as a **pull worker**: a new `ai-worker` console project on the AI station, using two new authenticated endpoints (`/ai/claim` with lease, `/ai/verdict`). The core never initiates AI traffic; the worker never touches SQLite directly. Tunnels (Tailscale, cloudflared, SSH reverse) were considered to restore core→AI reachability and rejected. *(The `/ai/claim`-with-lease endpoint was superseded 2026-09-11 by the read-only `GET /ai/next`.)* |
| 2026-09 | **Apply-stage mechanism amended** (§1): since the core cannot call the AI station, the apply-stage "synchronous LLM call" becomes a **priority claim** — apply-stage jobs jump the queue and `ai-worker` polls at its shortest interval while any are pending. Principle unchanged (a deliberate pause at delivery is acceptable); mechanism now matches the topology. |
| 2026-09 | **Apply assistant planned (phase 5)** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)): a fourth station — the personal terminal — runs a second MV3 extension that fills apply forms through an agentic tool loop (`memory_query` / `memory_write` / `fill`) against the AI station's `llama-server` reached over the LAN; the learning memory lives on the core behind new `/assistant/*` endpoints. Apply stays human-triggered (no new `JobState` in v1) and the human presses every submit. **Never both extensions in one browser** — the search extension matches `*://*/*` and would fight over the apply tabs. |
| 2026-09 | **Assistant guardrails and data sources** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §3–§5): the model sees a form inventory only (never raw HTML, never emits selectors); there is no submit tool; core keys stay in the extension; personal data comes from the job's resume text in the Fill prompt plus memory — deliberately no structured profile table; submit-time diffs and chat tips are the two learning channels. An `X-Client` role header distinguishes the two extensions (absent header = legacy search). |
| 2026-09-10 | **Phase 0 is documentation-only; implementation starts at phase 1**, which absorbs `ai-worker` + `/ai/claim` + `/ai/verdict` (no separate milestone). |
| 2026-09-10 | **Four clients, three role headers.** `search`, `worker` (new — `ai-worker`, restricted to `/ai/*`), `assistant`; the dashboard user authenticates via the existing login/API-key and sends no role header. Absent header = legacy search. |
| 2026-09-10 | **`ai-worker` runs manually; no polling loop** (§2.1). The timed lease is dropped in favor of a startup sweep that resets stale in-flight jobs *(pending round-2 confirmation)*. **Superseded 2026-09-11:** no sweep, no claim, no lease — read-only `GET /ai/next` + idempotent `POST /ai/verdict`. |
| 2026-09-10 | **Apply-stage priority claim removed** (§1). The core never tracks apply stage: the user opens `job-detail`, applies on the external site, and presses the manual Apply button (`server-operations.js` `apply(jobid)`). No exception to the golden rule remains. |
| 2026-09-10 | **Two-stage purge** (§6). Score < 50 purges immediately; Attention + near-miss (50–99) keep `Html`/`Content` until the verdict; `/ai/verdict` processing purges inadequate AI ranks. Per-job AI-status encoding open (round 2). *(Resolved 2026-09-11: `AiState` encoding + human-confirmed stage 2.)* |
| 2026-09-10 | **Verdict is effective** (§4.1). `AiScore` lives in separate additive columns (regex `Score` untouched) and participates in ranking — a deliberate, careful `Q_INDEX` v2 edit; the earlier "don't touch" note is overridden. Exact formula open (round 2). *(Resolved 2026-09-11: `FinalScore = W_r * Score + W_a * AiScore` — see below.)* |
| 2026-09-10 | **Local-only across all lanes** (§3). The resume is PII and the full resume text is sent for ranking too, so hosted LLM endpoints are unused; the provider-agnostic client remains for the future. |
| 2026-09-10 | **Quality by trial and error; feedback loop agreed in principle.** No golden set. User overrides of AI rankings will be recorded and injected into future ranking prompts (design round 2). *(Settled 2026-09-11: raw override log + unified memory with hybrid injection — see below.)* |
| 2026-09-10 | **Context cap 16k tokens, configurable** (32k acceptable); tuned by trial and error. |
| 2026-09-10 | **No backfill.** Old jobs are expired; reprocessing is manual: change the job's state, run `ai-worker`. |
| 2026-09-10 | **Duplicates are advisory** (§4.4). Dashboard warning, user-clearable; on confirmation the second posting moves to a new `Duplicated` state, which ranking must exclude. Mechanics open (round 2). *(Resolved 2026-09-11: mechanics deferred to phase 4.)* |
| 2026-09-10 | **Digest is dashboard-only statistics** (§4.5) — counts of new jobs, good jobs (regex) and strong jobs (AI verdict); no external notification channel. |
| 2026-09-10 | **Resume tailoring is selection-only end to end** (§4.3 / [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3). The summary becomes segmented pre-written variants; free-text summary/jobTitle generation is dropped. The user's text-box selection mechanism and `AiOptions ?? Options` fallback are unchanged. *(Partly superseded 2026-09-11: jobTitle gains a guarded title-only free-text exception, and the fallback becomes `Options.HumanEdited ? Options : (AiOptions ?? Options)`.)* |
| 2026-09-11 | **Phases 1+2 merged.** One `ai-worker` pass per job produces the semantic verdict AND the structured extraction; the former phase 2 is absorbed into phase 1, phases 3–5 keep their numbers (§7). |
| 2026-09-11 | **Two model calls per worker pass** (§6). Call 1 (always): verdict + extraction as one JSON-schema-constrained response. Call 2 (conditional, relevance ≥ threshold): resume-tailoring delta (phase 3). Both return in one idempotent `POST /ai/verdict`; rejected jobs never pay for tailoring. |
| 2026-09-11 | **Extraction storage on `Job` itself** (§4.2): additive columns `AiSalaryMin/Max`, `AiCurrency`, `AiPeriod`, `AiSeniority`, `AiWorkModel`, `AiContract`, `AiExperienceYears`, `AiSkills` (JSON, type-handler pattern); enums as names; no new table (1:1). |
| 2026-09-11 | **Final score blend** (§4.1): `FinalScore = W_r * Score + W_a * AiScore` computed at query time in `Q_INDEX` v2, never stored; initial `W_r = 0.35`, `W_a = 0.65`, weights in job-option settings (tunable, no rebuild); verdict-less jobs rank by regex `Score`. |
| 2026-09-11 | **Stage-2 purge is human-confirmed** (§6): a weak verdict only marks a purge candidate; `Html`/`Content` deletion is a dashboard action (single/bulk, pre-selected by threshold). Stage 1 (< 50) stays automatic. |
| 2026-09-11 | **No claim, no lease, no sweep** (§2.1). `GET /ai/next` is read-only (oldest `Pending`, job text + resume, no state change); idempotent `POST /ai/verdict` is the only write. Crash before verdict = still `Pending`, retried naturally. One worker at a time; a second worker only duplicates inference (last-write-wins). |
| 2026-09-11 | **`Job.AiState` encoding**: additive column, enum name as text; `Pending` (queue entry: Attention + near-miss 50–99) / `Processed` (set by `/ai/verdict` after `Pending` validation); NULL = outside the queue (no backfill honored). Non-`Pending` verdicts are rejected and logged. |
| 2026-09-11 | **Human/AI coexistence on resume context** ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §6): `HumanEdited` flag in `ResumeContext` (version bump; round-trips through `SimlpeSerialize`); precedence `Options.HumanEdited ? Options : (AiOptions ?? Options)`; AI writes only `AiOptions`; post-edit AI suggestions show as a diff with an explicit accept action. |
| 2026-09-11 | **jobTitle: fixed variants + guarded title-only free text** ([`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) §3): default is selection among pre-written headline variants; the model may propose free text for the title slot only — review-flagged, never auto-applied, server-side single-line/length cap. Raw `job.Title` copying is dropped. |
| 2026-09-11 | **`Duplicated` mechanics deferred to phase 4**; the advisory-only decision stands. |
| 2026-09-11 | **Unified memory subsystem** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §4): one table/API/retrieval mechanism with a `Scope` column (`resume`/`apply`/`ranking`), replacing the planned `apply_memory`; ranking keeps a separate raw override log, durable lessons distill into `Scope = ranking` rows. |
| 2026-09-11 | **Hybrid memory injection** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §4): deterministic pre-injection of high-confidence rows as a labeled, token-capped (~1–2k), stably-ordered data block + `memory_query` for exploration. Memory is data, never instructions; `UseCount` bumps only on applied values; chat writes visible and deletable; page text never enters memory unconfirmed. |
| 2026-09-11 | **`X-Client: search` ships in phase 1** — one-line extension change, single deployment; `X-API-Key` unchanged. |
| 2026-09-11 | **Assistant-mediated feedback UX** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §4): resume-tailoring and apply-form feedback both flow through the assistant chat; the model decides what to persist and instructs the assistant to write memory rows for future prompts. |
| 2026-09-12 | **Near-miss retention is a phase-1 code change** (§6). Today's purge threshold is `Score < MinEligibilityScore = 100` — the earlier "below 50, today's behavior unchanged" claim was wrong. The new stage-1 threshold is the configurable near-miss floor (e.g. 50/70), shared with AI-queue entry and shipped together with `AiState = Pending` in `EvaluateJobEligibility`; expected DB growth accepted; the Attention pass-mark stays 100 (the floor never changes `JobState`). |
| 2026-09-12 | **Verdict semantics are upsert** (§2.1), superseding the 2026-09-11 "non-Pending verdicts rejected" rule: `POST /ai/verdict` always updates after validating job existence and payload (AiScore 0–100, enum names, length caps); out-of-queue verdicts are logged informationally. Resolves the idempotent/last-write-wins contradiction; retry-after-lost-response is a no-op; manual reprocess = re-run the worker. |
| 2026-09-12 | **AiScore fixed at 0–100; FinalScore normalized** (§4.1): the model judges apply-worthiness (master resume + JD + JobOption weights); `RegexNorm = min(Score, ScoreCap)/ScoreCap×100` (`ScoreCap` setting, initial ~300); `FinalScore = W_r×RegexNorm + W_a×AiScore` on 0–100; decay applied post-blend; the `JobRanking.cs` mirror is updated in the same change. Regex-vs-AI divergence statistics are dropped (scale mismatch). |
| 2026-09-12 | **Ranking profile = master resume** (§4.1): the verdict prompt carries a stable, job-independent base resume. Open (round 3): storage source — settings text vs rendered default template. |
| 2026-09-12 | **Second-chance threshold is a setting** applied to `FinalScore` (0–100 scale). Open (round 3): promotion mechanics — dynamic top category in `Q_INDEX` v2 vs `JobState` change. |
| 2026-09-12 | **`llama-server` stays localhost through phase 3** (§3); LAN exposure (binding/proxy + home-LAN trust assumption) is decided in phase 5 — resolves the §2/§3 contradiction. **One worker at a time** is a standing convention, not enforced in code. |
| 2026-09-12 | **Open (round 3) queue:** master-resume storage source; near-miss promotion mechanics; confirmation of upsert guards + `ScoreCap` initial value. Deferred: queue membership rules (when `Pending` is set/exited, Revaluation re-queue, oldest-first key), poison-job error verdict, manual-reprocess dashboard action, roles-are-not-a-security-boundary note (cookie auth = dashboard), proxy hardening (rate limit / HSTS / IP allowlist for `/ai/*`), worker config core `ApiKey` + deployment story. |
