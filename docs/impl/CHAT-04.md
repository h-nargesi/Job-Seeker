# Chat 4 — Auth, `/ai/*`, phase-1 dashboard

**Status:** not started. **Depends on:** Chat 3.

## Goal

Per-client keys, worker HTTP API (call-1 payload), master-resume helper,
minimal job-detail AI UI. Ignore absent `delta` (F2). No `ai-worker` project.

## Design pointers

[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) (Program.cs D7, F3, F5,
job-detail, JobController D1.4/D8/D12, D15 keywords).
[`AI_INTEGRATION.md`](../AI_INTEGRATION.md) §2 / §2.1 / §6 (D13 is worker-side
Chat 5; core returns 404/400/5xx honestly).
[`docs/API.md`](../API.md) — update auth + new `/ai/*` when implementing.
Decision log D7, D13, D15, D16 inventory **not** until Chat 6.

## Files

- [`Program.cs`](../../core-decision-dotnet/Program.cs) — remove single
  `Auth:ApiKey`. Table `Auth:ApiKeys:Dashboard` / `:Search` / `:Worker`.
  Key = role + path rules: search → `/decision/*`; worker → `/ai/*`;
  dashboard → everything. `FixedTimeEquals`. `X-Client` **logging only**
  (never an allow/deny `if`). Production fail-fast: missing **Dashboard**
  only; missing Search/Worker warn. Dev: keys optional, auth off. Keep the
  table data-driven so Chat 7 adds `Assistant` as a **row**, not a rewrite.
  Login password = Dashboard key.
- New `Controllers/Ai.cs` (or equivalent) — `GET /ai/next`,
  `POST /ai/verdict`. Next: job text, master resume text, `keywords`
  `{category, score, title}[]` (`reject` excluded, `FetchAll` order),
  `fingerprint`, `settings` including `aipassmark`. Verdict: validate
  ranges (decision log 2026-09-15); absent `delta` ignored; call
  `ApplyAiVerdict`.
- New reusable prune/strip helper (F5): render `Views/resume.cshtml` with
  default `ResumeContext`, HtmlAgilityPack strip (same idea as
  `GetTextContent`), lazy in-memory cache, restart-invalidated, 16k cap
  from the top.
- [`Controllers/Job.cs`](../../core-decision-dotnet/Controllers/Job.cs) —
  `Revaluate?jobid=` force path (ignore HumanEdited; keep Rejected/Applied
  guard; hide if `Content == null`; prepare to clear `AiOptions` +
  `ResumeText.proposal` — live survives). `POST /job/promote`, re-queue
  action (`AIError`/`NotApprovedAI` → `AiPending` only; hide if no Content).
- [`Views/job-detail.cshtml`](../../core-decision-dotnet/Views/job-detail.cshtml)
  (+ CSS if needed) — badges for new states; AiScore; extraction display;
  re-queue, force Revaluate, emergency promote. **No** phase-6 filters/bulk
  purge/digest.
- [`appsettings.json`](../../core-decision-dotnet/appsettings.json) skeleton
  for `Auth:ApiKeys:*` (empty values).
- Tests: auth path table; verdict HTTP validation; promote/re-queue guards.

## Must not

Build `ai-worker`. Activate call-2 / inventory. Phase-6 dashboard work.
Gating on `X-Client` header. Redo `agent-extension` `X-Client: search`.

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`

Manual: production-shaped config refuses start without Dashboard key.

## Done

Worker key reaches only `/ai/*`; Search key cannot hit `/ai`; dashboard
shows AI fields/buttons; `/ai/next` ships data only (no finished prompt).

## Starter prompt

```
Implement Chat 4 of the AI playbook.

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md, docs/impl/CHAT-04.md,
docs/AI_PHASE1_NOTES.md (Program.cs D7, F3, F5, job-detail, JobController),
docs/AI_INTEGRATION.md §2 and §2.1, docs/API.md.

Do only Chat 4. Stop at Done. Do not create ai-worker. Do not implement
call-2 delta application beyond ignoring absent delta (F2).

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
