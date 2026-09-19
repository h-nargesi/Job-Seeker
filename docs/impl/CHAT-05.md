# Chat 5 — `ai-worker` call 1

**Status:** done. **Depends on:** Chat 4.

## Goal

New console project: pull `GET /ai/next`, JSON-schema call 1 (verdict +
extraction), `POST /ai/verdict`. Call 2 not implemented. Memory slots in
the prompt are **empty reserved** (F1).

## Design pointers

[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) (ai-worker section, Rubric v1,
D6, D13, D17, F1). [`AI_INTEGRATION.md`](../AI_INTEGRATION.md) §2.1 / §6.
Decision log: two calls per pass (call 2 = Chat 6); one worker at a time
is convention, not mutex. Stack: C# console on Linux — see
[`AI_IMPLEMENTATION.md`](../AI_IMPLEMENTATION.md) **Stack**.

## Files

- New `ai-worker/` **.NET 8 C#** console at repo root; add to
  `Job Seeker.sln`. Not Python.
- Optional `ai-worker.sh` (env + exec), same idea as `job-seeker.sh`.
  Publish RID: `linux-x64 --self-contained` (Chat 5 may land the script;
  publish is the deploy contract even if the script waits).
- `Llm` config (appsettings + env): `BaseUrl`, `Model`, `Core` (core base
  URL), `CoreApiKey`, `Rubric`, `Temperature` (default 0.2), `Seed` (fixed).
  **No** `Enabled` key (D17).
- Plain `HttpClient`: OpenAI-compatible chat; `response_format` JSON schema
  for call 1. Headers: `X-API-Key` = worker key; `X-Client: worker` for logs.
- Loop `GET /ai/next` until empty. Prompt blocks, stable order: rubric →
  keywords → [reserved ranking-memory] → resume → JD. Tail-truncate JD under
  16k. Inject `{{keywords}}` into Rubric v1.
- Retries: two model-output (parse/schema) failures then error verdict
  (`AiVerdict = Error`). **Connection** failure: abort run, write nothing.
  Timeout 120 s per model call.
- D13: next non-200/network → abort run. Verdict 404 → log + continue;
  400 → abort loudly; 5xx/network → abort.
- Tests: prompt block order; schema; D13 branching (HttpMessageHandler
  fakes). Do not require a live GPU.

## Must not

Call 2 / `Llm:RubricTailor` (Chat 6). `memory[]` on verdict. Mutex vs
assistant. Polling daemon / Windows service. Python worker. A shell
script that *is* the worker (launcher only).

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`
plus any `ai-worker` test project you add.

## Done

Solution build includes `ai-worker`. A fake `/ai/next` + llama can complete
call 1 and post a verdict. Absent `delta` accepted by core (F2).

## Starter prompt

```
Implement Chat 5 of the AI playbook.

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md, docs/impl/CHAT-05.md,
docs/AI_PHASE1_NOTES.md (ai-worker, Rubric v1, D6 D13 D17 F1),
docs/AI_INTEGRATION.md §2.1 and §6.

Do only Chat 5. Stop at Done. Do not implement call 2.

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
