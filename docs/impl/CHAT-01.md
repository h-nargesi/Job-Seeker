# Chat 1 — Schema and types

**Status:** not started. **Depends on:** nothing.

## Goal

Land the AI data model. Compiles; no ranking SQL, no `/ai/*`, no worker.

## Design pointers

[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) (Core JobState + Schema +
AppSetting). [`AI_INTEGRATION.md`](../AI_INTEGRATION.md) §4.1 / §7 / D18.
[`AI_RESUME_TAILORING.md`](../AI_RESUME_TAILORING.md) §3 (`ResumeText` shape).
Decision log 2026-09-15, 09-17, 09-18 (D3, D18), 09-19 (`ResumeText`, no
`AiTitle`).

## Files

- [`database/structure/job.sql`](../../database/structure/job.sql) — additive
  columns: `AiScore`, `AiVerdict`, `AiReason`, `AiSeniority`, `AiSalaryMin`,
  `AiSalaryMax`, `AiCurrency`, `AiPeriod`, `AiWorkModel`, `AiContract`,
  `AiExperienceYears`, `AiSkills` (JSON), `AiOptions` (JSON), `ResumeText`
  (JSON). **No** `AiState`, **no** `AiTitle`.
- New [`database/structure/app-setting.sql`](../../database/structure/app-setting.sql)
  — `AppSetting (Key TEXT PRIMARY KEY, Value TEXT)` seeds `floor=70`,
  `aipassmark=60`, `scorecap=300`, `w_regex=0.35`, `w_ai=0.65`.
- [`database/installation.sh`](../../database/installation.sh) — add
  `sqlite3 data.sqlite3 < structure/app-setting.sql;`
- [`Analyze/Models/JobState.cs`](../../core-decision-dotnet/Analyze/Models/JobState.cs)
  — `Saved, Revaluation, NotApprovedRegex, AiPending, NotApprovedAI, AIError,
  Attention, Rejected, Applied`.
- New enums (names locked): `AiVerdict` (`StrongMatch, Match, Possible,
  NoMatch, Error`), `AiSeniority`, `AiWorkModel`, `AiContract`, `AiPeriod`
  — lists in phase-1 notes.
- [`Analyze/Models/Job.cs`](../../core-decision-dotnet/Analyze/Models/Job.cs)
  — matching properties (`AiOptions` is `ResumeContext`; `ResumeText` new
  type: slot map `live` / `proposal` / `status`).
- [`Database/SqliteTypeHandlers.cs`](../../core-decision-dotnet/Database/SqliteTypeHandlers.cs)
  — enum-name handlers for new enums; JSON handlers for `AiSkills` /
  `ResumeText`.
- New `Database/Business/AppSettingBusiness.cs` — typed getters, defaults if
  absent, **no cache**. Do not put these keys in `JobOptionSettings`.

## Must not

Change `Q_INDEX`, eligibility writes, auth, views, or worker. Do not add
`ALTER` migrations. Do not glob `structure/` in `installation.sh`.

## Validate

`dotnet build "Job Seeker.sln"`. Fix compile breaks from the `NotApproved`
rename (tests/SQL string literals) **only enough to compile** — behavior
updates are Chat 2–3. Prefer `nameof(JobState.*)` in SQL.

## Done

Schema files exist; `Job`/`JobState`/handlers/`AppSettingBusiness` compile;
fresh-install path includes `app-setting.sql`.

## Starter prompt

```
Implement Chat 1 of the AI playbook.

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md (rules + slice index only),
docs/impl/CHAT-01.md, docs/AI_PHASE1_NOTES.md (schema / JobState /
AppSetting), docs/AI_RESUME_TAILORING.md §3.

Do only Chat 1. Stop at Done. Do not start Chat 2.
Do not rewrite design docs. Append AI_DECISION_LOG.md only if a new lock
is required.

Validate: dotnet build "Job Seeker.sln"
```
