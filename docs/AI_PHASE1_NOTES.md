# AI phase 1 — implementation notes

> Verified code touchpoints for the phase-1 implementation of the sequential
> state machine (design: [`AI_INTEGRATION.md`](AI_INTEGRATION.md) §4.1, §7;
> decisions: [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) 2026-09-15,
> 2026-09-17, 2026-09-18).
> Symbol-level pointers, deliberately no line numbers — they rot. Written
> 2026-09-15 during the design session; re-verify each item before editing.
> Amended 2026-09-17: the open ambiguities are resolved and folded in
> (`AIError` state, AI enum sets, rubric v1, content-change compare,
> `Saved` resurrection, worker ops) plus forward-compatibility constraints
> F1–F6 — same-day decision-log entries are authoritative. Amended
> 2026-09-18: design-review decisions D1–D10 folded in (revaluation scope
> + Attempts-reset law, per-job Revaluate, verdict fingerprint,
> `AppSetting` table, `Q_CLEAN_ATTENTION` blend keying, Temperature/Seed
> determinism, per-client keys, emergency promote) — see the 2026-09-18
> decision-log entries. Same-day second batch D11–D17 folded in below:
> dashboard category layout (D11), re-queue source-state guard (D12),
> worker core-error protocol (D13), RubricTailor v1 (D14), `keywords`/
> inventory payload formats (D15/D16), no `Enabled` config key (D17),
> and the `installation.sh` schema-task correction. Amended 2026-09-19:
> two-layer tailoring (`ResumeText`, no `AiTitle`, inventory full-text
> on editable slots). Amended 2026-09-19: F3 = key⇒role table (not
> `X-Client` gating); `Q_CLEAN_NOT_APPROVED` `WHERE` is three states,
> re-queue button remains two (D12).

## Core (core-decision-dotnet)

- **`Analyze/Models/JobState.cs`** — current enum: `Saved, Revaluation,
  NotApproved, Attention, Rejected, Applied`. Becomes `Saved, Revaluation,
  NotApprovedRegex, AiPending, NotApprovedAI, AIError, Attention, Rejected,
  Applied` (2026-09-17: `AIError` for poison jobs — error verdicts bypass
  the passmark gate). No order-dependent logic may be introduced (F4).
- **`Analyze/JobEligibilityHelper.cs`** — the regex gate. `MinEligibilityScore`
  const becomes the `AppSetting` floor key (default 70 — D3); both usages (the
  `clear_content` purge check and the `EvaluateEligibility` pass check) read
  it. State writes in `EvaluateJobEligibility` (`!eligibility → NotApproved`,
  `else → Attention`) become `below floor → NotApprovedRegex` / `floor+ →
  AiPending`. The `user_changes = State > Attention` guard (order-dependent)
  becomes an explicit `State is Rejected or Applied` check. Persistence goes
  through `UpdateEvaluation`. Phase-3 law recorded now (D1): when
  `job.Options?.HumanEdited == true`, skip the `Options` overwrite (keep
  stored context) — `Score`/`Log` still regenerate; applies to every eval
  path except the per-job force button (D1.4). The file is ~409
  lines — already over the ~400-line budget (AGENTS.md); split by content
  while touching it.
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
  (2026-09-17); within `Attention`, verdict-less (manually promoted) jobs
  rank by `RegexNorm` — COALESCE-style (D8). The blend expression is a
  shared C# SQL fragment (scorecap + weights as SQL parameters from
  `AppSetting`) used by `Q_INDEX` v2 and `Q_CLEAN_ATTENTION` alike: the
  latter's top-100 Html-retention subquery orders by `FinalScore`, not raw
  `Score` (D5). Dashboard layout (D11): category numbers + display caps —
  `Attention = 1` (12 rows, `FinalScore`; verdict-less promoted jobs
  COALESCE to `RegexNorm`), `AiPending = 2` (6, `RegexNorm`),
  `NotApprovedAI = 3` (6, `FinalScore`), `Applied`/`Rejected = 4` (3 each,
  unchanged), `NotApprovedRegex = 5` (6, `RegexNorm`), `AIError = 6` (3,
  `RegexNorm`), ELSE `Saved`/`Revaluation = 12` (1, unchanged); page order
  top→bottom Attention, AiPending, NotApprovedAI, Applied/Rejected,
  NotApprovedRegex, AIError, Saved/Revaluation — actionable rows on top,
  bulk/informational bands below. Cleanup family: `Q_CLEAN` (deletes old
  non-Applied — bounds queue history), `Q_CLEAN_ATTENTION` (top-100 Html
  retention), `Q_CLEAN_NOT_APPROVED` (content purge `WHERE` → `NotApprovedRegex`,
  `NotApprovedAI` **and `AIError`** — today it matches only `NotApproved`;
  D12's "exactly these two" is the re-queue **button**, not this `WHERE`
  — 2026-09-19 lock). New typed methods: `FetchNextAiPending` (oldest by
  `JobID`, read-only, **`Content IS NOT NULL` defensive filter** — D12;
  response carries the job text, the master resume text, a JobOption-derived
  `keywords` field — 2026-09-17; a standard JSON array of
  `{category, score, title}` objects, `reject` category excluded, stable
  cached `FetchAll` order — D15 — and the content `fingerprint`, D2),
  `ApplyAiVerdict` (upsert + guarded state transition; `AiVerdict = Error`
  transitions to `AIError` bypassing the passmark gate; recomputes the
  fingerprint from the stored `Content` — on mismatch upsert +
  informational log, **no state transition**, `Content == null` = mismatch
  — D2), `RequeueJob` (`AIError`/`NotApprovedAI` → `AiPending` **only** —
  never `Attention` (the per-job Revaluate, D1.4, covers that), `Rejected`,
  `Applied`, `Saved`; disabled/hidden when `Content == null` — D12),
  `PromoteJob`
  (emergency manual promote, D8: `AiPending`/`NotApprovedAI`/`AIError` →
  `Attention` only; appends `Manually promoted (emergency) — <date>` to
  `Log`, bumps `ModifiedOn`; no synthetic `AiScore`). `RunRevaluateProcess`
  gains the 2026-09-17 resurrection rule: a purged `NotApprovedRegex` job
  whose stored `Score` passes the new floor → `State = Saved` **plus
  `Attempts = 0, Tries = NULL`** (D4 unified law: any deliberate return to
  `Saved` resets Attempts; natural re-scrape; `Score` survives purge —
  `Q_REMOVE_HTML` nulls only `Html`/`Content`). Revaluation machinery (D1):
  `Q_FETCH_FROM`/`Q_FETCH_FROM_COUNT` use the positive list
  `State IN ('Attention','AiPending','NotApprovedAI','AIError') AND
  Content IS NOT NULL AND ModifiedOn <= @date` (the old
  `State != 'Revaluation'` condition is absorbed; `Saved`/`Rejected`/
  `Applied` excluded — frozen history); `FetchFrom` keeps the transient
  `Revaluation` mark (double-pickup guard); `ResetRevaluations` becomes
  `UPDATE Job SET State = 'Saved', Attempts = 0, Tries = NULL WHERE
  State = 'Revaluation'` (crashed runs recover via browser revisit; the
  reset avoids the `Attempts >= 4` zombie trap in `Q_FETCH_FIRST`).
- **`Database/Business/AgencyBusiness.cs`** — dashboard stats count
  `Attention`; semantics shift to AI-approved (no code change expected).
- **`Analyze/JobRanking.cs`** — decay-weight mirror of `Q_INDEX`; must be
  updated in the same change as v2.
- **`Analyze/Models/Job.cs`** — `SetHtml` already derives `Content` via
  `GetTextContent`; that is the `/ai/next` job text (no new derivation).
- **`Database/Business/AppSettingBusiness.cs`** (new, D3) — the five
  phase-1 settings (`floor=70`, `aipassmark=60`, `scorecap=300`,
  `w_regex=0.35`, `w_ai=0.65`) live in the new `AppSetting` table, **not**
  in job-option settings (`JobOptionSettings.cs` is untouched). Typed
  getters with defaults for absent keys; **no cache** — read per use.
  Consumers: the regex gate (floor — replaces `MinEligibilityScore` in
  both usages), `/ai/next` (`aipassmark`, phase 3), `Q_INDEX` v2 +
  `Q_CLEAN_ATTENTION` (scorecap + weights as SQL parameters). Editing: the
  existing job-options SQL console (`POST /job/setting`).
- **`Program.cs`** — inline auth middleware (`Authorized`: `X-API-Key` or
  dashboard cookie) becomes per-client-key (D7): `Auth:ApiKeys:Dashboard` /
  `:Search` / `:Worker` in config/env (`Assistant` is added in phase 5),
  each compared with the existing FixedTimeEquals helper. **Key = role with
  path rules:** `search` → `/decision/*` (+ `/decision/scopes`), `worker` →
  `/ai/*`, `dashboard` → everything; `X-Client` stays an
  informational/logging header only; the single `Auth:ApiKey` mode is
  removed (clean cutover). Production fail-fast: only a missing
  **Dashboard** key refuses startup; missing Search/Worker keys log a
  warning. Keep the key⇒role table data-driven so the phase-5 `assistant`
  key + `/assistant/*` is a registration, not a middleware rewrite (F3).
- **`Views/job-detail.cshtml`** — state badge colors per `JobState`; new
  states + re-queue button (`AIError → AiPending` too) + AiScore display +
  per-job Revaluate (D1.4) and emergency promote (D8) buttons.
  Phase-1 UI is minimal (2026-09-17): extracted-value display, AiScore,
  re-queue button — filters, bulk purge and digest changes are phase 6.
- **`Controllers/Job.cs`** — `Revaluate` gains an optional `jobid` query
  param (D1.4): single-job force re-eval (ignores the `HumanEdited` guard;
  keeps the explicit `Rejected`/`Applied` guard; from phase 3 clears
  `AiOptions` and `ResumeText.proposal` (`live` survives; no `AiTitle`);
  disabled/hidden when `Content == null`; no param =
  the global process unchanged; no locking vs a concurrent global run). New
  `Promote` action (D8): `POST /job/promote?jobid=` → `PromoteJob`.
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
  (JSON via type-handler; enums as names), plus the phase-3 columns
  `AiOptions` (JSON `ResumeContext`, existing type handler) and
  `ResumeText` (JSON slot map: `live` / `proposal` / `status`) from day
  one (single release — D18, 2026-09-18, amended 2026-09-19 — no
  `AiTitle`).
  **No `AiState` column** (queue is
  `State = AiPending`). Enum member sets locked 2026-09-17 — identical in
  the model's JSON schema, the verdict validation and the columns:
  `AiVerdict`: `StrongMatch, Match, Possible, NoMatch, Error`; `AiSeniority`:
  `Junior, Mid, Senior, Lead, Unknown`; `AiWorkModel`: `Onsite, Hybrid,
  Remote, Unknown`; `AiContract`: `Permanent, B2B, Temporary, Unknown`;
  `AiPeriod`: `Hour, Day, Month, Year, Unknown`.
- **`app-setting.sql`** (new, D3) — `AppSetting (Key TEXT PRIMARY KEY,
  Value TEXT)` + seeds `floor=70`, `aipassmark=60`, `scorecap=300`,
  `w_regex=0.35`, `w_ai=0.65`. **`installation.sh` does NOT pick up
  `structure/` files automatically** (2026-09-18 correction — the script
  enumerates each file explicitly): the phase-1 change must add
  `sqlite3 data.sqlite3 < structure/app-setting.sql;` to
  `database/installation.sh`, or the settings table never exists and the
  floor/passmark reads silently fall back to defaults (no ordering
  dependency); `job-option.sql` is untouched.
- Fresh database on implementation — no row migration (phase-1 scope;
  phases 1–3 ship as a single release — rollout-granularity decision
  (D18), 2026-09-18, amended 2026-09-19 — so the phase-3 `AiOptions` and
  `ResumeText` columns enter `job.sql` from day one; no `ALTER TABLE`
  path; no `AiTitle`).

## ai-worker (new console project, repo root)

- Plain `HttpClient` OpenAI-compatible chat client; JSON-schema-constrained
  call 1 (verdict + extraction); optional `delta` field reserved for the
  phase-3 call 2 (validation ignores it — F2); loop `GET /ai/next` until
  empty; 2 retries per job then error verdict (`AiVerdict = Error`, state
  `AIError`); single-flight; config via `Llm` section (appsettings +
  env vars): `BaseUrl/Model/Core` + `CoreApiKey` (the core's worker key,
  D7) + `Temperature` (default 0.2) and `Seed` (fixed value) sent with
  every request (D6 — `llama-server` supports both; re-runs must be stable
  so `AiScore` cannot flip around `aiPassmark` without an input change).
  No `Enabled` kill-switch key (D17 — the worker runs manually; a
  kill-switch is a dead code path).
- Ops (2026-09-17): 120 s timeout per model call; **connection failures
  abort the whole run and write nothing** (a GPU outage must never
  mass-produce verdicts) — only model-output failures (invalid
  JSON/schema) retry and then error-verdict that one job. Context budget
  under the 16k cap: rubric (~1k) + master resume in full + JD remainder;
  an over-long JD is truncated at its tail (requirements live early).
- **Core-error protocol (D13):** `GET /ai/next` — any network error or
  non-200 aborts the whole run; `POST /ai/verdict` — 404 (job deleted
  between fetch and verdict) → log + continue with the next job; 400
  (validation rejection = worker bug) → abort the run loudly; 5xx or
  network error → abort the run. Loop-safety: prevents an endless loop
  on the same oldest `AiPending` job.
- Prompts are assembled worker-side from labeled blocks in stable order —
  rubric → keywords → [reserved slots: phase-5 **confirmed** ranking-memory
  snapshot at run start (call 1); **confirmed** resume-memory snapshot at
  run start (call 2)] → resume →
  JD (F1) — keeping the shared prefix cache-friendly; `/ai/next` ships
  data only (job text, master resume, `keywords` — a standard JSON array
  of `{category, score, title}` objects, `reject` category excluded,
  stable cached `FetchAll` order — D15), never a finished
  prompt. The worker does not emit memory writes.
- **Add to `Job Seeker.sln`** so `dotnet build "Job Seeker.sln"` (the
  primary validation gate) covers it. Language is **C# / .NET 8**, not
  Python — the AI station is a Linux terminal; that is a RID
  (`linux-x64 --self-contained` publish + optional `ai-worker.sh`
  launcher), not a stack change
  ([`AI_IMPLEMENTATION.md`](AI_IMPLEMENTATION.md) Stack, 2026-09-19).

## Extension (agent-extension)

- **`controllers/core-messaging.js`** — **already shipped** (2026-09-18
  audit): `BuildHeaders` sends both `X-API-Key` and `X-Client: search`
  (`core-messaging.js`, the `BuildHeaders` constant) — the decided
  one-line phase-1 change is a no-op; don't re-do it. The popup API-Key
  field takes the **Search** key (`Auth:ApiKeys:Search`, D7) — pasted by
  the user; no further code change.
- Tests live in `tests/core-messaging.test.js` and already assert the
  `X-Client` header.

## Tests (core-decision-dotnet.Tests)

Expected touch points: `JobRankingTests.cs` (categories/blend),
`JobEligibilityHelperTests.cs`, `PhaseAGoldenTests.cs`, `PhaseBNewApiTests.cs`,
`GetFirstJobTests.cs`, `SalaryScoreTests.cs` (state renames, floor setting).
Add: verdict-guard tests (validation, state-transition-only-from-`AiPending`)
and the re-scrape content-change rule; per the 2026-09-18 decisions also
the revaluation-scope predicate, fingerprint mismatch path, promote
source-state guards, auth role/path table, settings defaults and the
resurrection Attempts reset.

## Rubric v1 (2026-09-17)

**Superseded 2026-09-24.** Live text is `Llm:Fixed` (mina / trunk first
block) plus request-only `Llm:Rubric` / `Llm:RubricTailor` in the worker's
appsettings (env-overridable). The worker no longer substitutes
`{{keywords}}` — keywords sit in the trunk as `## KEYWORD PRIORITIES`.
Historical v1 draft follows; do not paste it back into appsettings.

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

## RubricTailor v1 (2026-09-18, D14; amended 2026-09-19)

**Superseded 2026-09-24** — live `Llm:Fixed` + request-only
`Llm:RubricTailor` no longer inline `{{keywords}}`. Historical draft follows.

The call-2 system prompt; ships as `Llm:RubricTailor` in the worker's
appsettings (env-overridable like `Llm:Rubric`), sharing the same
`Temperature`/`Seed` (D6). Like call 1 it consumes `{{keywords}}` — a
deliberate extension of the declared call-2 input list (rubric +
inventory + context + JD): legal because `keywords` ships in `/ai/next`
from phase 1 and is a constant (the global `JobOption` table), so the
call-2 prefix stays cache-stable.

> You tailor the candidate's resume for one job in two layers. Layer 1 is
> **selection**: choose among the inventory of pre-written blocks
> (selectors), matching the job's keywords and seniority; prefer fewer,
> highly relevant blocks; select only items that exist in the inventory.
> Layer 2 is **proposed wording** for the closed slot set only — resume
> title, summary paragraph, and existing job-description bullets. Rules:
> never invent employers, dates, or new experience entries; reword a slot
> only when selection is not enough; never emit HTML; title is a single
> line. Proposed wording is a suggestion — it does not apply until the
> human accepts it. Weight the candidate's keyword priorities:
> {{keywords}}. When uncertain, keep the current selection and omit a
> text proposal.

## Payload details (2026-09-18, D15/D16; inventory amended 2026-09-19)

- **`keywords`** (`GET /ai/next`, phase 1): a standard JSON array of
  `{category, score, title}` objects derived from `JobOption`; the
  `reject` category is excluded (regex-side concern, wasted tokens);
  stable cached order (`FetchAll` order) for determinism.
- **Inventory caps** (call 2, phase 3): editable text slots (title,
  summary, job-description bullets) ship **full** current template text;
  other items keep excerpt ≤ 120 chars; no item-count cap. The 16k
  per-call cap is unchanged; JD tail-truncation absorbs overflow.

## Forward-compatibility constraints (2026-09-17)

| # | Constraint (protects a later phase) |
|---|-------------------------------------|
| F1 | Prompts assembled in ai-worker from labeled blocks; `/ai/next` ships data only (phase 3 extends the payload; phase 5 injects **confirmed** ranking-memory into call 1 and **confirmed** resume-memory into call 2, snapshotted at worker **run start** — 2026-09-19). No `memory[]` on `POST /ai/verdict`. |
| F2 | `/ai/verdict` validation ignores an absent `delta`, never rejects it (phase 3 activates it) |
| F3 | Key⇒role⇒paths table data-driven in `Program.cs` `Authorized` (D7: matching `Auth:ApiKeys:*` is the role; `X-Client` logging only, never an allow/deny `if`). Phase 5 adds one row: `Assistant` → `/assistant/*` + `GET /decision/scopes`. Absent header grants nothing (2026-09-19 lock). |
| F4 | No order-dependent `JobState` logic — explicit `is Rejected or Applied` checks only (phase 4 adds `Duplicated`) |
| F5 | Master-resume prune/strip built as a reusable helper (phases 3/5 reuse the pattern) |
| F6 | Superseded 2026-09-18 (D7): per-client keys ship in phase 1 — `Auth:ApiKeys:Dashboard/Search/Worker`; key ⇒ role with path rules in `Program.cs`; `X-Client` informational/logging only. Phase 5 adds `Auth:ApiKeys:Assistant` as a registration (F3) |

## Known follow-ups

- `AI_INTEGRATION.md` §4.5 digest counts ("good jobs = regex") predate the
  state machine; redefinition deferred to phase 6 (2026-09-17) — which also
  gains an `AiPending` counter for data-driven floor tuning via
  `AppSetting` (2026-09-18, D9).
- Phase-3 delta contract and `/ai/next` payload extension decided
  2026-09-17, two-layer text overlay 2026-09-19 — see the decision log;
  F1/F2 already anticipate the payload extension.
