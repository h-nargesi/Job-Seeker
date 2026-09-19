# Chat 7 — Phase 5 core: memory and `/assistant/*`

**Status:** not started. **Depends on:** Chat 6.

## Goal

Unified memory table + assistant HTTP API + F3 `Assistant` key. Worker
snapshots confirmed memory at **run start**. No browser extension yet.

## Design pointers

[`AI_APPLY_ASSISTANT.md`](../AI_APPLY_ASSISTANT.md) §4–§6.
[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) F1 / F3.
Decision log 2026-09-19 (memory write policy, confirm-then-inject,
`memorycap`, closed ranking `FieldKey` list). [`GLOSSARY.md`](../GLOSSARY.md).

## Files

- New `database/structure/memory.sql` (name as fits) + **explicit** line in
  `installation.sh`. Columns per apply-assistant §4. Seed `memorycap=500`
  on `AppSetting`.
- New `MemoryBusiness` — insert always succeeds (no storage cap). Ranking
  `FieldKey` closed list; unknown rejected. Precedence: correction > tip;
  exact domain > `*`; then `UseCount`; then newest `UpdatedAt`. Query
  returns **confirmed** only. `UseCount` bump only when a value was applied
  (assistant will call this in Chat 8; expose a typed method now).
- [`Program.cs`](../../core-decision-dotnet/Program.cs) — register
  `Auth:ApiKeys:Assistant` → `/assistant/*` + `GET /decision/scopes`. No
  `/ai/*`, no `/decision/take`. Absent `X-Client` grants nothing.
- New `Controllers/Assistant.cs`:
  - `GET /assistant/jobs` — `Attention` jobs + `resume_text` (same
    selection precedence + `ResumeText.live` strip; **no** HTML) +
    pending-proposal **flag**.
  - `POST /assistant/applied` — same as `POST /job/apply`; idempotent;
    `job.Log` records source.
  - `/assistant/memory` CRUD including confirm / edit / delete.
- Worker F1: at **run start**, snapshot confirmed `Scope=ranking` into call
  1 reserved block and confirmed `Scope=resume` into call 2; cap injected
  confirmed rows by `memorycap`. Still no `memory[]` on verdict. Worker
  does not write memory.
- Tests: role paths; ranking key reject; unconfirmed never in snapshot;
  applied idempotency.

## Must not

Build `assistant-extension/`. Encrypt memory. Distillation jobs. Phase-6
dashboard memory UI (UI is the assistant, Chat 8).

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`

Update [`docs/API.md`](../API.md) for `/assistant/*`.

## Done

Assistant key can list jobs and CRUD memory; Search/Worker keys cannot.
Worker call-1/2 prompts can include a snapshot (empty if no rows).

## Starter prompt

```
Implement Chat 7 of the AI playbook.

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md, docs/impl/CHAT-07.md,
docs/AI_APPLY_ASSISTANT.md §4–§6, docs/AI_PHASE1_NOTES.md F1 and F3,
docs/GLOSSARY.md, decision log 2026-09-19 memory policy.

Do only Chat 7. Stop at Done. Do not create assistant-extension/.

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
