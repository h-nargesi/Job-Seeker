# AI integration

> **Status: design proposal + recorded decisions — not implemented.** This
> document consolidates everything about using AI in this system: the agreed
> design for adding an LLM (locally hosted by default: llama.cpp `llama-server`
> with a GGUF model such as `Qwen3-30B-A3B-Q5_K_M`), the integration points by
> priority, and the decisions taken so far (§7). No AI code exists yet; nothing
> here describes current behavior.

Related: [`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md) (phase 3 detail).

## 1. The golden rule: the LLM never sits in the browser loop

`POST /decision/take` is the path the extension blocks on, and the whole
scraping loop is paced by its response time. LLM inference takes seconds;
the loop must stay deterministic and millisecond-fast. So the system gets
two lanes:

```
browser loop (fast, deterministic)          background AI lane (slow, smart)
──────────────────────────────────          ──────────────────────────────────
regex scoring (unchanged)              →    queue → llama-server → DB update
Job saved with State = Attention       →   verdict, extraction, resume delta
```

The regex gate stays first and cheap — it rejects most jobs and costs nothing.
The LLM only sees the survivors. This alone cuts the number of model calls by
roughly an order of magnitude versus scoring every scraped job.

The implementation vehicle already exists as a pattern:
`JobEligibilityHelper.RunRevaluateProcess` is a background worker that pulls
jobs from the DB one by one and processes them with progress tracking. An
AI enrichment worker is the same shape: a `Channel<Job>` with a single
consumer hosted service, calling the model and writing results back.

**One bounded exception — the apply stage.** When the loop is parked on a
single job page and the next step is producing the tailored resume for that
one job, a synchronous LLM call of a few seconds paces nothing: no scanning
loop is waiting, and a human reviews the result anyway. The golden rule
protects search-result processing (dozens of pages back to back); it does not
forbid a deliberate pause at the point of delivery.

## 2. Runtime setup

- llama.cpp's `llama-server` exposes an OpenAI-compatible endpoint
  (`/v1/chat/completions`) on localhost. It runs as a separate process next
  to `job-seeker`; the model never ships inside the server.
- Client: a plain `HttpClient` + JSON. No new package dependencies.
- Suggested configuration in `appsettings.json`:

```json
"Llm": {
  "BaseUrl": "http://localhost:8082/v1",
  "Model": "Qwen3-30B-A3B-Q5_K_M",
  "Enabled": true
}
```

- Concurrency: **one** in-flight request. `llama-server` processes requests
  sequentially; a single-consumer queue also keeps ordering debuggable.
- **Provider-agnostic by construction.** Because the client is a plain
  `HttpClient` speaking the OpenAI chat-completions protocol, any compatible
  endpoint works and switching is a config change only:
  - *Local:* `llama-server`, Ollama, LM Studio.
  - *Hosted:* OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini.
  - Hosted endpoints need an `"ApiKey"` entry in the `Llm` config — supplied
    via user-secrets / environment variables, never committed.

## 3. Integration points, by priority

### 3.1 Semantic verdict / re-ranking on `Attention` jobs

Today `EvaluateEligibility` is exact regex matching: a job that mentions
".NET" once scores like a genuinely .NET-centric role. The LLM judges only
jobs that already passed the regex gate (`State = Attention`):

> Is this genuinely a senior full-stack role? Real seniority? Direct hire or
> staffing agency? Is relocation supported?

Output is a small JSON verdict — `{ relevance: 0-100, seniority, verdict,
reason }` — with `reason` appended to `job.Log` (already markdown-rendered on
the dashboard). Highest value, lowest risk: no schema changes, no loop
changes.

### 3.2 Structured extraction

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

### 3.3 Resume tailoring (decided end-to-end flow)

The strongest free-text use case — see the companion doc
[`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md). The decided flow:

1. The browser reaches the job page; the server scrapes the job description
   and scores it. The existing regex path already builds the initial
   `ResumeContext` (`Job.Options`) from the JD's keywords.
2. The LLM refines that context into a reviewable delta stored in
   `Job.AiOptions`. Bulk scanning uses the background lane; a synchronous
   call is acceptable at the apply stage (§1 exception).
3. The user opens `/job/resume?jobid=...` (or downloads the rendered HTML via
   `/job/resume64`) and **prints from the browser** (Brave). The view already
   carries `@media print` CSS, so it is print-ready as served. No server-side
   PDF conversion and no new `print` browser command are planned.

### 3.4 Cross-platform deduplication

A cheap prefilter (same normalized title + company across agencies) produces
candidate pairs; the LLM judges whether two postings are the same job; the
dashboard merges them. Closes the known gap that one posting listed on both
Indeed and LinkedIn gets scored twice.

### 3.5 Daily digest / notification

An overnight batch summary ("24 new jobs today, 3 strong matches: ...") served
on the dashboard. Closes the notification gap with no external service — and
matches the "leave the system running" usage pattern.

## 4. Where NOT to use the LLM

| Place | Why |
|-------|-----|
| `Command[]` / `TrendsCheckpoint` | Must stay deterministic and instant. An LLM error means a lost/confused browser tab, and debugging nondeterministic control flow is misery. |
| Page detection regexes (`LoginPage`, `SearchPage`, ...) | Fast and adequate. The model only adds latency and nondeterminism where none is needed. |
| `LanguageIsMatch` | The dictionary lookup is instant and free. Leave it alone. |

## 5. Practical notes

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
  LLM before the purge decision or retain content for that band.

## 6. Phasing

| Phase | Deliverable | Risk |
|-------|-------------|------|
| 1 | LLM verdict + reason on `Attention` jobs | Minimal: touches only `Log` |
| 2 | Structured extraction columns + dashboard filters | Additive schema only; `Q_INDEX` untouched |
| 3 | Resume tailoring delta (`Job.AiOptions`) | Human review gate before `Applied` |
| 4 | Cross-platform dedup + daily digest | Read-only over existing data |

## 7. Decision log

| Date | Decision |
|------|----------|
| 2026-09 | **CloudConvert retired.** The HTML→PDF conversion path in `wwwroot/scripts/resume-pdf.js` is no longer used and its `API_KEY` was removed from the repo. Final delivery of the resume is a manual print from the browser (Brave) against `/job/resume`, which is print-ready via its `@media print` rules. The file remains loaded by `Views/layout.cshtml` as dead code pending cleanup. |
| 2026-09 | **No `print` PageAction.** The extension command vocabulary (`go/open/fill/click/recheck/close/wait/reload`) stays as is; turning the resume into PDF/paper is a human step, not a browser command. |
| 2026-09 | **Resume-from-JD at the job stage is the target flow** (§3.3): regex `ResumeContext` first, LLM delta on top, human review, then print from `/job/resume`. |
| 2026-09 | **Apply-stage exception to the golden rule** (§1): a synchronous LLM call is acceptable when a single job page is in flight and a human reviews the output before printing. |
| 2026-09 | **Provider-agnostic client** (§2): plain `HttpClient` + OpenAI-compatible endpoint; local (`llama-server`, Ollama, LM Studio) or hosted (OpenRouter, Groq, DeepSeek, OpenAI, Anthropic, Gemini) chosen by config only. |
