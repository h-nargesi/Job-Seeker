# AI phase 1 — implementation notes

> Verified code touchpoints for the phase-1 implementation of the sequential
> state machine (design: [`AI_INTEGRATION.md`](AI_INTEGRATION.md) §4.1, §7;
> decisions: [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) 2026-09-15).
> Symbol-level pointers, deliberately no line numbers — they rot. Written
> 2026-09-15 during the design session; re-verify each item before editing.

## Core (core-decision-dotnet)

- **`Analyze/Models/JobState.cs`** — current enum: `Saved, Revaluation,
  NotApproved, Attention, Rejected, Applied`. Becomes `Saved, Revaluation,
  NotApprovedRegex, AiPending, NotApprovedAI, Attention, Rejected, Applied`.
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
  `content_changed` flag). `include_state` (passed to `UpdateScrapedJob`)
  needs its predicate updated for the new states.
- **`Analyze/Stepstone/StepstonePageJob.cs`** — duplicates `LoadJob` and the
  approval-keyed save-button click; mirror both changes here.
- **`Analyze/Indeed/IndeedPageJob.cs`** — writes `NotApproved` directly;
  rename.
- **`Database/Business/JobBusiness.cs`** — `Q_INDEX` (categories, decay;
  enum-name literals + the `Relocation` log marker) gains the v2 blend:
  `RegexNorm = min(Score, ScoreCap)/ScoreCap×100`, `FinalScore` for
  `Attention`/`NotApprovedAI` only, `RegexNorm` for `AiPending`. Cleanup
  family: `Q_CLEAN` (deletes old non-Applied — bounds queue history),
  `Q_CLEAN_ATTENTION` (top-100 Html retention), `Q_CLEAN_NOT_APPROVED`
  (content purge → cover `NotApprovedRegex` and `NotApprovedAI`). New typed
  methods: `FetchNextAiPending` (oldest by `JobID`, read-only),
  `ApplyAiVerdict` (upsert + guarded state transition), `RequeueJob`.
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
  restricted to `/ai/*`; absent header = legacy search).
- **`Views/job-detail.cshtml`** — state badge colors per `JobState`; new
  states + re-queue button + AiScore display.
- **Master resume service** (new) — render `Views/resume.cshtml` with a
  fixed default `ResumeContext`, prune the superset + strip text with
  HtmlAgilityPack (pattern: `JobEligibilityHelper.GetTextContent`),
  in-memory cache, lazy, restart-invalidated, 16k-token cap from the top.

## Schema (database/structure)

- **`job.sql`** — no AI columns yet; add the additive set: `AiScore`,
  `AiVerdict`, `AiReason`, `AiSeniority`, `AiSalaryMin/Max`, `AiCurrency`,
  `AiPeriod`, `AiWorkModel`, `AiContract`, `AiExperienceYears`, `AiSkills`
  (JSON via type-handler; enums as names). **No `AiState` column** (queue is
  `State = AiPending`).
- **`job-option.sql`** — seed the new settings (floor=70, aipassmark=60,
  scorecap=300, w_regex=0.35, w_ai=0.65).
- Fresh database on implementation — no row migration.

## ai-worker (new console project, repo root)

- Plain `HttpClient` OpenAI-compatible chat client; JSON-schema-constrained
  call 1 (verdict + extraction); optional `delta` field reserved for the
  phase-3 call 2; loop `GET /ai/next` until empty; 2 retries per job then
  error verdict; single-flight; config via `Llm` section (appsettings +
  env vars).
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

## Known follow-ups

- `AI_INTEGRATION.md` §4.5 digest counts ("good jobs = regex") predate the
  state machine; redefine the counts when phase 4 lands.
