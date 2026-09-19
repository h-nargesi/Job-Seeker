# Chat 3 — Ranking SQL and verdict persistence

**Status:** not started. **Depends on:** Chat 2. **Highest-risk slice.**

## Goal

Dashboard ranking v2, cleanup family, typed AI persistence methods. No HTTP
controllers yet (Chat 4 can call these methods).

## Design pointers

[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) (`JobBusiness`, `JobRanking`,
D2/D5/D8/D11/D12). [`AI_INTEGRATION.md`](../AI_INTEGRATION.md) §4.1.
AGENTS.md “Things that bite” (`Q_INDEX`, enum names, Relocation marker).
Decision log 2026-09-17 (`AIError`), 09-18 (D1–D5, D8, D11, D12).

## Files

- [`Database/Business/JobBusiness.cs`](../../core-decision-dotnet/Database/Business/JobBusiness.cs)
  — `Q_INDEX` v2 categories + caps (D11): Attention=1 (12, `FinalScore`;
  verdict-less COALESCE `RegexNorm`), AiPending=2 (6, `RegexNorm`),
  NotApprovedAI=3 (6, `FinalScore`), Applied/Rejected=4 (3 each),
  NotApprovedRegex=5 (6, `RegexNorm`), AIError=6 (3, `RegexNorm`), ELSE=12
  (1). Shared C# SQL fragment for blend: `RegexNorm =
  min(Score, ScoreCap)/ScoreCap×100`; `FinalScore` only for
  `Attention`/`NotApprovedAI`; scorecap + weights as **SQL parameters** from
  `AppSetting`. Keep Relocation log marker.
  - `Q_CLEAN` unchanged idea (old non-Applied).
  - `Q_CLEAN_ATTENTION` top-100 Html retention orders by `FinalScore` (D5).
  - `Q_CLEAN_NOT_APPROVED` `WHERE` → `NotApprovedRegex`, `NotApprovedAI`,
    **and** `AIError` (D12 lock: three states here; re-queue button is Chat 4).
  - Typed: `FetchNextAiPending` (oldest `JobID`, `Content IS NOT NULL`),
    `ApplyAiVerdict` (upsert columns; state only from `AiPending`;
    `AiVerdict = Error` → `AIError` bypass passmark; fingerprint SHA-256 of
    whitespace-normalized `Content`, computed not stored; mismatch or
    `Content == null` → upsert + log, **no** state change), `RequeueJob`,
    `PromoteJob`.
  - Revaluation: `Q_FETCH_FROM` / `Q_FETCH_FROM_COUNT` positive list
    Attention/AiPending/NotApprovedAI/AIError + Content + date. Resurrection:
    purged `NotApprovedRegex` whose `Score` passes floor → `Saved` +
    `Attempts = 0, Tries = NULL`. `ResetRevaluations` → `Saved` + same reset.
- [`Analyze/JobRanking.cs`](../../core-decision-dotnet/Analyze/JobRanking.cs)
  — decay-weight **mirror** of `Q_INDEX` (keep in sync). Blend if mirrored
  in C#.
- [`Analyze/Models/JobListItem.cs`](../../core-decision-dotnet/Analyze/Models/JobListItem.cs)
  if category/score fields need extending.

## Must not

Drive-by refactors of unrelated queries. Do not add `AiController` here.
Do not change job-option SQL. Do not use integer enums in SQL.

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`

Update [`JobRankingTests.cs`](../../core-decision-dotnet.Tests/JobRankingTests.cs).
Add: verdict guards (only from `AiPending`), fingerprint mismatch, promote
source states, resurrection Attempts reset, revaluation predicate.

## Done

Index categories match D11; blend uses AppSetting params; `ApplyAiVerdict`
never promotes on fingerprint mismatch; `AIError` excluded from `FinalScore`
ordering.

## Starter prompt

```
Implement Chat 3 of the AI playbook (highest-risk: Q_INDEX v2).

Read: AGENTS.md (Things that bite), docs/AI_IMPLEMENTATION.md,
docs/impl/CHAT-03.md, docs/AI_PHASE1_NOTES.md (JobBusiness, JobRanking,
D2 D5 D8 D11 D12), docs/AI_INTEGRATION.md §4.1.

Do only Chat 3. Stop at Done. Do not add HTTP /ai endpoints.

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
