# AI phase 1 — implementation notes

> Verified code touchpoints for the phase-1 implementation of the sequential
> state machine (design: [`AI_INTEGRATION.md`](AI_INTEGRATION.md) §4.1, §7;
> decisions: [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) 2026-09-15).
> Symbol-level pointers, deliberately no line numbers — they rot. Written
> 2026-09-15 during the design session; re-verify each item before editing.
> Amended 2026-09-17: the open ambiguities are resolved and folded in
> (`AIError` state, AI enum sets, rubric v1, content-change compare,
> `Saved` resurrection, worker ops) plus forward-compatibility constraints
> F1–F6 — same-day decision-log entries are authoritative.

## Core (core-decision-dotnet)

- **`Analyze/Models/JobState.cs`** — current enum: `Saved, Revaluation,
  NotApproved, Attention, Rejected, Applied`. Becomes `Saved, Revaluation,
  NotApprovedRegex, AiPending, NotApprovedAI, AIError, Attention, Rejected,
  Applied` (2026-09-17: `AIError` for poison jobs — error verdicts bypass
  the passmark gate). No order-dependent logic may be introduced (F4).
- **`Analyze/JobEligibilityHelper.cs`** — the regex gate. `MinEligibilityScore`
  const becomes the job-option floor setting (default 70); both usages (the
  `clear_content` purge check and the `EvaluateEligibility` pass check) read
  it. State writes in `EvaluateJobEligibility` (`!eligibility → NotApproved`,
  `else → Attention`) become `below floor → NotApprovedRegex` / `floor+ →
  AiPending`. The `user_changes = State > Attention` guard (order-dependent)
  becomes an explicit `State is Rejected or Applied` check. Persistence goes
  through `UpdateEvaluation`. The file is ~409 lines — already over the
  ~400-line budget (AGENTS.md); split by content while touching it.
- **`Analyze/Pages/JobPage.cs`** — browser-loop follow-ups. `IssueCommand`
  early-returns `[]` for `NotApproved` and fires `JobFallow` commands on
  regex approval (today: `state == JobState.Attention`); both re-key to the
  new states/regex-approval result. `LoadJob` is where the re-scrape
  content-change compare belongs (before `Job.SetHtml`; thread a
  `content_changed` flag). Compare rule (2026-09-17):
  `Normalize(newText) != Normalize(stored Content)` with Normalize =
  collapse consecutive whitespace to one space + trim ends; no hash column.
  `include_state` (passed to `UpdateScrapedJob`) needs its predicate
  updated for the new states.
- **`Analyze/Stepstone/StepstonePageJob.cs`** — duplicates `LoadJob` and the
  approval-keyed save-button click; mirror both changes here.
- **`Analyze/Indeed/IndeedPageJob.cs`** — writes `NotApproved` directly;
  rename.
- **`Database/Business/JobBusiness.cs`** — `Q_INDEX` (categories, decay;
  enum-name literals + the `Relocation` log marker) gains the v2 blend:
  `RegexNorm = min(Score, ScoreCap)/ScoreCap×100`, `FinalScore` for
  `Attention`/`NotApprovedAI` only, `RegexNorm` for `AiPending`; `AIError`
  gets its own category ordered by `RegexNorm`, never `FinalScore`
  (2026-09-17). Cleanup family: `Q_CLEAN` (deletes old non-Applied — bounds
  queue history), `Q_CLEAN_ATTENTION` (top-100 Html retention),
  `Q_CLEAN_NOT_APPROVED` (content purge → must cover `NotApprovedRegex`,
  `NotApprovedAI` **and `AIError`** — today it matches only `NotApproved`).
  New typed methods: `FetchNextAiPending` (oldest by `JobID`, read-only;
  response carries the job text, the master resume text and a
  JobOption-derived `keywords` field — 2026-09-17), `ApplyAiVerdict`
  (upsert + guarded state transition; `AiVerdict = Error` transitions to
  `AIError` bypassing the passmark gate), `RequeueJob` (`AiPending` from
  `AIError` and others). `RunRevaluateProcess` gains the 2026-09-17
  resurrection rule: a purged `NotApprovedRegex` job whose stored `Score`
  passes the new floor → `State = Saved` (natural re-scrape; `Score`
  survives purge — `Q_REMOVE_HTML` nulls only `Html`/`Content`).
  Revaluation machinery to keep compatible: `RunRevaluateProcess` /
  `FetchFrom` / `ResetRevaluations` (transient `Revaluation` state; selects
  jobs with `Content IS NOT NULL`).
- **`Database/Business/AgencyBusiness.cs`** — dashboard stats count
  `Attention`; semantics shift to AI-approved (no code change expected).
- **`Analyze/JobRanking.cs`** — decay-weight mirror of `Q_INDEX`; must be
  updated in the same change as v2.
- **`Analyze/Models/Job.cs`** — `SetHtml` already derives `Content` via
  `GetTextContent`; that is the `/ai/next` job text (no new derivation).
- **`Analyze/Models/JobOptionSettings.cs`** — extend for floor, aipassmark,
  scorecap, w_regex, w_ai.
- **`Program.cs`** — inline auth middleware (`Authorized`: `X-API-Key` or
  dashboard cookie) is where the `X-Client` role check lands (worker
  restricted to `/ai/*`; absent header = legacy search). Keep the role
  table data-driven so the phase-5 `assistant` role + `/assistant/*` is a
  registration, not a middleware rewrite (F3).
- **`Views/job-detail.cshtml`** — state badge colors per `JobState`; new
  states + re-queue button (`AIError → AiPending` too) + AiScore display.
  Phase-1 UI is minimal (2026-09-17): extracted-value display, AiScore,
  re-queue button — filters, bulk purge and digest changes are phase 6.
- **Master resume service** (new) — render `Views/resume.cshtml` with a
  fixed default `ResumeContext`, prune the superset + strip text with
  HtmlAgilityPack (pattern: `JobEligibilityHelper.GetTextContent`),
  in-memory cache, lazy, restart-invalidated, 16k-token cap from the top.
  Build the prune + strip as a reusable helper, not inline: phase 3
  derives the block inventory and phase 5 renders per-job resume text with
  the same pattern (F5).

## Schema (database/structure)

- **`job.sql`** — no AI columns yet; add the additive set: `AiScore`,
  `AiVerdict`, `AiReason`, `AiSeniority`, `AiSalaryMin/Max`, `AiCurrency`,
  `AiPeriod`, `AiWorkModel`, `AiContract`, `AiExperienceYears`, `AiSkills`
  (JSON via type-handler; enums as names). **No `AiState` column** (queue is
  `State = AiPending`). Enum member sets locked 2026-09-17 — identical in
  the model's JSON schema, the verdict validation and the columns:
  `AiVerdict`: `StrongMatch, Match, Possible, NoMatch, Error`; `AiSeniority`:
  `Junior, Mid, Senior, Lead, Unknown`; `AiWorkModel`: `Onsite, Hybrid,
  Remote, Unknown`; `AiContract`: `Permanent, B2B, Temporary, Unknown`;
  `AiPeriod`: `Hour, Day, Month, Year, Unknown`.
- **`job-option.sql`** — seed the new settings (floor=70, aipassmark=60,
  scorecap=300, w_regex=0.35, w_ai=0.65).
- Fresh database on implementation — no row migration.

## ai-worker (new console project, repo root)

- Plain `HttpClient` OpenAI-compatible chat client; JSON-schema-constrained
  call 1 (verdict + extraction); optional `delta` field reserved for the
  phase-3 call 2 (validation ignores it — F2); loop `GET /ai/next` until
  empty; 2 retries per job then error verdict (`AiVerdict = Error`, state
  `AIError`); single-flight; config via `Llm` section (appsettings +
  env vars).
- Ops (2026-09-17): 120 s timeout per model call; **connection failures
  abort the whole run and write nothing** (a GPU outage must never
  mass-produce verdicts) — only model-output failures (invalid
  JSON/schema) retry and then error-verdict that one job. Context budget
  under the 16k cap: rubric (~1k) + master resume in full + JD remainder;
  an over-long JD is truncated at its tail (requirements live early).
- Prompts are assembled worker-side from labeled blocks in stable order —
  rubric → keywords → [reserved slot: phase-5 memory injection] → resume →
  JD (F1) — keeping the shared prefix cache-friendly; `/ai/next` ships
  data only (job text, master resume, `keywords`), never a finished
  prompt.
- **Add to `Job Seeker.sln`** so `dotnet build "Job Seeker.sln"` (the
  primary validation gate) covers it.

## Extension (agent-extension)

- **`controllers/core-messaging.js`** — `BuildHeaders` adds `X-API-Key`;
  add `X-Client: search` here (the decided one-line phase-1 change).
- Tests live in `tests/core-messaging.test.js` (header assertions).

## Tests (core-decision-dotnet.Tests)

Expected touch points: `JobRankingTests.cs` (categories/blend),
`JobEligibilityHelperTests.cs`, `PhaseAGoldenTests.cs`, `PhaseBNewApiTests.cs`,
`GetFirstJobTests.cs`, `SalaryScoreTests.cs` (state renames, floor setting).
Add: verdict-guard tests (validation, state-transition-only-from-`AiPending`)
and the re-scrape content-change rule.

## Rubric v1 (2026-09-17)

The default system prompt; ships as `Llm:Rubric` in the worker's
appsettings (env-var overridable — tunable without rebuild). The worker
injects the JobOption-derived `keywords` payload at `{{keywords}}`:

> You are a skeptical senior recruiter judging one job posting against the
> candidate's master resume. Score apply-worthiness 0–100 (0 = do not
> apply). Weight the candidate's keyword priorities: {{keywords}}.
> Criteria: (1) Is this genuinely a senior full-stack/.NET role (C#,
> ASP.NET Core, Angular/TypeScript, SQL) — not a role that merely mentions
> the stack once? (2) Real seniority: ownership, design, mentoring — not
> title inflation. (3) Direct hire vs staffing agency. (4) Work model and
> location reality; relocation support. (5) Stated compensation vs the
> candidate's market. When in doubt, score lower. Also extract the
> structured fields exactly as specified by the schema; use "Unknown" when
> the posting does not say.

## Forward-compatibility constraints (2026-09-17)

| # | Constraint (protects a later phase) |
|---|-------------------------------------|
| F1 | Prompts assembled in ai-worker from labeled blocks; `/ai/next` ships data only (phase 3 extends the payload; phase 5 injects a memory block) |
| F2 | `/ai/verdict` validation ignores an absent `delta`, never rejects it (phase 3 activates it) |
| F3 | `X-Client` role registration data-driven in `Program.cs` (phase 5 adds `assistant` + `/assistant/*` as a registration) |
| F4 | No order-dependent `JobState` logic — explicit `is Rejected or Applied` checks only (phase 4 adds `Duplicated`) |
| F5 | Master-resume prune/strip built as a reusable helper (phases 3/5 reuse the pattern) |
| F6 | Single shared `X-API-Key`; no key⇒role assumptions (per-client keys are phase 5) |

## Known follow-ups

- `AI_INTEGRATION.md` §4.5 digest counts ("good jobs = regex") predate the
  state machine; redefinition deferred to phase 6 (2026-09-17).
- Phase-3 delta contract and `/ai/next` payload extension decided
  2026-09-17 — see the "Phase-3 ambiguities resolved" row in
  [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md); F1/F2 already anticipate
  both.
