# AI integration

> **Status: design proposal + recorded decisions — not implemented.** This
> document consolidates everything about using AI in this system: the decided
> deployment topology (§2), the agreed design for adding an LLM (locally
> hosted by default: llama.cpp `llama-server` with a GGUF model such as
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

The queue lives on the core (DB-backed claim/lease — §2); the consumer is
`ai-worker`, a separate program on the AI station. The in-core precedent for
a one-by-one background pass with progress tracking is
`JobEligibilityHelper.RunRevaluateProcess`.

**One bounded exception — the apply stage.** When the loop is parked on a
single job page and the next step is producing the tailored resume for that
one job, a delay of a few seconds paces nothing: no scanning loop is waiting,
and a human reviews the result anyway. Because the core cannot reach the AI
station (§2), the earlier "synchronous LLM call" mechanism becomes a
**priority claim**: apply-stage jobs jump the queue and the worker polls at
its shortest interval while any are pending — near-synchronous, same
guarantees. The golden rule protects search-result processing (dozens of
pages back to back); it does not forbid a deliberate pause at the point of
delivery.

## 2. Deployment topology (Phase 0 — decided)

Four stations, three of them outbound-only (the fourth is planned):

```
                ┌────────────────────────────────┐
                │  core: job-seeker (.NET)       │  public, SSL (reverse proxy),
                │  SQLite + AI queue + API       │  X-API-Key + X-Client role
                └─▲──────────────▲─────────────▲─┘
   POST /decision/take             │            │  POST /ai/claim · /ai/verdict
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
  endpoint and nothing else; the core cannot see it back.
- **Personal terminal (phase 5 — planned)** — the assistant's host: the
  user's own desktop, running the planned `assistant-extension`
  ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)). Outbound-only to the
  core (`X-Client: assistant`) and, over the LAN, to the AI station's
  `llama-server`. Never the search extension and the assistant in one
  browser — the search extension matches `*://*/*` and would fight over the
  apply tabs.

### 2.1 The ai-worker loop

```
ai-worker (AI station)                      core (job-seeker)
────────────────────────────                ────────────────────────────────
loop:                                       jobs pending verdict, apply-stage
  POST /ai/claim   ──────────────────────►  first, oldest first; response
  (X-API-Key)                                carries the job text; marks the
                                             job in-flight with a lease
  run local llama-server (localhost)
  POST /ai/verdict ──────────────────────►  validates, writes verdict /
  (X-API-Key, job id, JSON)                 extraction / delta, clears lease
  sleep: seconds while apply-stage
  jobs pending, minutes otherwise           lease expiry re-queues the job
```

- Claim/lease (not a plain list read) so an AI-station crash or network drop
  re-queues work automatically; verdict posts are idempotent by job id.
- The worker **never touches SQLite directly** — only the two endpoints.
- **Rejected alternative: tunnels** (Tailscale, cloudflared, SSH reverse)
  would restore core→AI reachability and allow an in-core worker calling
  `llama-server` remotely. Rejected for Phase 0: extra infrastructure to keep
  alive, and the pull model needs none.

## 3. Runtime setup

- llama.cpp's `llama-server` exposes an OpenAI-compatible endpoint
  (`/v1/chat/completions`) on the AI station's localhost. The model never
  ships inside `job-seeker`; the core never talks to it at all — `ai-worker`
  is its only client.
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
    via environment variables / secrets, never committed.

## 4. Integration points, by priority

At a glance (details in the subsections below):

| # | Point | Lane | Impact | Risk |
|---|-------|------|--------|------|
| 4.1 | Semantic verdict / re-ranking | background | **High** — fixes the sharpest weakness: lexical scoring treats one ".NET" mention like a .NET-centric role | Minimal — touches only `Log`; no schema, no loop changes |
| 4.2 | Structured extraction | background | **High** — replaces guesswork heuristics (`EvaluateSalaryScore`); enables dashboard filters on salary/work-model reality | Low — additive columns only; `Q_INDEX` untouched |
| 4.3 | Resume tailoring delta | background + priority claim at apply (§1) | **Highest end value** — per-job customization with no fabrication risk (selection only) | Medium — new `Job.AiOptions` column + human review gate |
| 4.4 | Cross-platform deduplication | background | **Medium** — one posting listed on two agencies stops being scored twice | Low |
| 4.5 | Daily digest | overnight batch | **Low–medium** — closes the notification gap with no external service | Low — read-only over existing data |

### 4.1 Semantic verdict / re-ranking on `Attention` jobs

Today `EvaluateEligibility` is exact regex matching: a job that mentions
".NET" once scores like a genuinely .NET-centric role. The LLM judges only
jobs that already passed the regex gate (`State = Attention`):

> Is this genuinely a senior full-stack role? Real seniority? Direct hire or
> staffing agency? Is relocation supported?

Output is a small JSON verdict — `{ relevance: 0-100, seniority, verdict,
reason }` — with `reason` appended to `job.Log` (already markdown-rendered on
the dashboard). Highest value, lowest risk: no schema changes, no loop
changes.

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

Stored as **new additive columns** on `Job`, enabling dashboard filters by
work model / salary reality instead of approximate score.

> **Do not touch `JobBusiness.Q_INDEX`.** The ranking SQL silently depends on
> enum-name string literals and the `Relocation` log marker (see
> "Things that bite" in [`AGENTS.md`](../AGENTS.md)). Additive columns are
> safe; edits to the query are not.

### 4.3 Resume tailoring (decided end-to-end flow)

The strongest free-text use case — see the companion doc
[`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md). The decided flow:

1. The browser reaches the job page; the server scrapes the job description
   and scores it. The existing regex path already builds the initial
   `ResumeContext` (`Job.Options`) from the JD's keywords.
2. The LLM refines that context into a reviewable delta stored in
   `Job.AiOptions`. Bulk scanning uses the background lane; at the apply
   stage the job is priority-claimed so the delta arrives within seconds
   (§1).
3. The user opens `/job/resume?jobid=...` (or downloads the rendered HTML via
   `/job/resume64`) and **prints from the browser** (Brave). The view already
   carries `@media print` CSS, so it is print-ready as served. No server-side
   PDF conversion and no new `print` browser command are planned.

### 4.4 Cross-platform deduplication

A cheap prefilter (same normalized title + company across agencies) produces
candidate pairs; the LLM judges whether two postings are the same job; the
dashboard merges them. Closes the known gap that one posting listed on both
Indeed and LinkedIn gets scored twice.

### 4.5 Daily digest / notification

An overnight batch summary ("24 new jobs today, 3 strong matches: ...") served
on the dashboard. Closes the notification gap with no external service — and
matches the "leave the system running" usage pattern.

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
- **Context length.** Cap job content sent to the model (~8–12k tokens) to
  keep inference fast; truncate from the top, requirements live early.
- **Purge interaction.** `EvaluateJobEligibility` purges `Html`/`Content`
  when a job is rejected or scores below `MinEligibilityScore` (100). If the
  LLM should give near-misses (score 50–99) a second chance, either run the
  LLM before the purge decision or retain content for that band. The
  `/ai/claim` response must carry the job text, so content retention for
  AI-pending jobs is a hard prerequisite either way.

## 7. Phasing

| Phase | Deliverable | Risk |
|-------|-------------|------|
| 0 | Topology (§2): `ai-worker` skeleton + `/ai/claim` & `/ai/verdict` with claim/lease | Small new console project; core change is two authenticated endpoints |
| 1 | LLM verdict + reason on `Attention` jobs | Minimal: touches only `Log` |
| 2 | Structured extraction columns + dashboard filters | Additive schema only; `Q_INDEX` untouched |
| 3 | Resume tailoring delta (`Job.AiOptions`) | Human review gate before `Applied` |
| 4 | Cross-platform dedup + daily digest | Read-only over existing data |
| 5 | Apply assistant on a personal terminal ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)) | New extension + `/assistant/*` endpoints; the human presses every submit |

Best ratio of value to risk is **phase 1**: it corrects the most precise
weakness of the current pipeline — lexical regex scoring — with the smallest
possible code change. The highest end value sits in **phase 3**: the delta
pattern means the model can only *select* among pre-written blocks, which on a
factual document is the line between tailoring and fabrication. Phase 0 is the
enabling infrastructure both sit on.

## 8. Decision log

| Date | Decision |
|------|----------|
| 2026-09 | **CloudConvert retired.** The HTML→PDF conversion path in `wwwroot/scripts/resume-pdf.js` is no longer used and its `API_KEY` was removed from the repo. Final delivery of the resume is a manual print from the browser (Brave) against `/job/resume`, which is print-ready via its `@media print` rules. The file remains loaded by `Views/layout.cshtml` as dead code pending cleanup. |
| 2026-09 | **No `print` PageAction.** The extension command vocabulary (`go/open/fill/click/recheck/close/wait/reload`) stays as is; turning the resume into PDF/paper is a human step, not a browser command. |
| 2026-09 | **Resume-from-JD at the job stage is the target flow** (§4.3): regex `ResumeContext` first, LLM delta on top, human review, then print from `/job/resume`. |
| 2026-09 | **Apply-stage exception to the golden rule** (§1): a delay of a few seconds is acceptable when a single job page is in flight and a human reviews the output before printing. |
| 2026-09 | **Provider-agnostic client** (§3): plain `HttpClient` + OpenAI-compatible endpoint; local (`llama-server`, Ollama, LM Studio) or hosted (OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini) chosen by config only. |
| 2026-09 | **Phase 0 topology: three stations, pull model** (§2). Core (`job-seeker`, public SSL, `X-API-Key`) is the only reachable server. The search agent's host is a long-running, outbound-only browser desktop — a station, not a server, no code changes. The AI station (GPU box) is unreachable from the core, so the AI lane runs as a **pull worker**: a new `ai-worker` console project on the AI station, using two new authenticated endpoints (`/ai/claim` with lease, `/ai/verdict`). The core never initiates AI traffic; the worker never touches SQLite directly. Tunnels (Tailscale, cloudflared, SSH reverse) were considered to restore core→AI reachability and rejected. |
| 2026-09 | **Apply-stage mechanism amended** (§1): since the core cannot call the AI station, the apply-stage "synchronous LLM call" becomes a **priority claim** — apply-stage jobs jump the queue and `ai-worker` polls at its shortest interval while any are pending. Principle unchanged (a deliberate pause at delivery is acceptable); mechanism now matches the topology. |
| 2026-09 | **Apply assistant planned (phase 5)** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md)): a fourth station — the personal terminal — runs a second MV3 extension that fills apply forms through an agentic tool loop (`memory_query` / `memory_write` / `fill`) against the AI station's `llama-server` reached over the LAN; the learning memory lives on the core behind new `/assistant/*` endpoints. Apply stays human-triggered (no new `JobState` in v1) and the human presses every submit. **Never both extensions in one browser** — the search extension matches `*://*/*` and would fight over the apply tabs. |
| 2026-09 | **Assistant guardrails and data sources** ([`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md) §3–§5): the model sees a form inventory only (never raw HTML, never emits selectors); there is no submit tool; core keys stay in the extension; personal data comes from the job's resume text in the Fill prompt plus memory — deliberately no structured profile table; submit-time diffs and chat tips are the two learning channels. An `X-Client` role header distinguishes the two extensions (absent header = legacy search). |
