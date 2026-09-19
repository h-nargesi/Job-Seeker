# AI implementation playbook

> Execution slices for phases **1 + absorbed-2, 3, 5, 5.5**. Design is locked
> elsewhere; this file is **how to implement across chats**, not a second
> decision log.
>
> **Do not re-decide** the design in implementation chats. Append
> [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md) only if a new lock is required
> (never edit history).

Related: [`AI_INTEGRATION.md`](AI_INTEGRATION.md),
[`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md),
[`AI_RESUME_TAILORING.md`](AI_RESUME_TAILORING.md),
[`AI_APPLY_ASSISTANT.md`](AI_APPLY_ASSISTANT.md),
[`GLOSSARY.md`](GLOSSARY.md).

## Method

One sequential slice per chat. Stop at that slice’s **Done**. Open a **new**
chat for the next slice with the starter prompt in `docs/impl/CHAT-0N.md`.

- Phase **2** is not a milestone: extraction ships with phase 1 (one worker
  pass). Dashboard filters over extraction columns are **phase 6** (out).
- D18: phases **1–3** are one schema/release. `AiOptions` and `ResumeText`
  columns land in Chat 1 even though tailoring code is Chat 6.
- Chats are slices of one design, not independent products.

```
Chat 1 schema → 2 regex gate → 3 ranking SQL → 4 auth/API/UI
  → 5 worker call 1 → 6 tailoring → 7 memory API
  → 8 assistant extension → 9 Compose
```

## Hard rules (all chats)

- F1–F5: [`AI_PHASE1_NOTES.md`](AI_PHASE1_NOTES.md) (F6 superseded by D7).
- No LLM in `Command[]` / `TrendsCheckpoint` / page-detection regexes /
  `LanguageIsMatch` ([`AI_INTEGRATION.md`](AI_INTEGRATION.md) §5).
- No order-dependent `JobState` logic (F4). Explicit `Rejected`/`Applied`.
- Enum columns store the **name** as text. SQL parameters: `.ToString()`.
- Fresh database on first AI schema; **no** `ALTER TABLE` migration.
- `installation.sh` lists SQL files **explicitly** (does not glob `structure/`).
- Human presses every apply **submit**. No submit tool. No `memory[]` on
  `POST /ai/verdict`. Worker does not write memory.
- Search and assistant extensions **never** share a browser.
- Do not re-add `X-Client: search` in `agent-extension` — already shipped.
- File-size budget ~400 lines; split by content when touching oversized files.
- After C# edits: `dotnet build "Job Seeker.sln"`. Tests:
  `dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`.

## Stack (worker, assistant, Linux)

Locked 2026-09-19 — Linux on the AI station is **not** a language reason.

- **`ai-worker`** is a **.NET 8 C# console** at repo root, in
  `Job Seeker.sln`. Not Python (no venv, no transformers). It is a
  `HttpClient` to the core and to localhost `llama-server` (llama.cpp
  does inference). Chat 5 builds it.
- **Linux deploy:** `dotnet publish ai-worker -c Release -r linux-x64
  --self-contained`, then copy. No SDK required on the GPU box. Optional
  thin launcher like `job-seeker.sh` (`ai-worker.sh`: env + exec). Shell
  is **not** a second worker. Manual run, no systemd / polling (D17).
- **`assistant-extension/`** is Chrome MV3 **vanilla JS**, no build,
  patterned on `agent-extension/` (Chat 8). Not a Python UI.

## Out of this program

Phase **4** (dedup + digest). Phase **6** (extraction filters, bulk purge,
digest/`AiPending` counters). Per-site ATS adapters. File uploads.
Structured profile table. PII encryption of memory. Worker polling loop /
`Enabled` kill-switch.

## How to run a chat

1. Paste the **Starter prompt** from that slice file.
2. Implement only that slice. Do not start the next.
3. Run the slice’s validation commands.
4. Leave a one-line note in the chat: slice N done / blocked.

Current tree: Chats 1–7 done (AI schema, regex gate, ranking SQL, auth/`/ai/*`,
`ai-worker` call 1, call 2 tailoring, memory + `/assistant/*` + worker
snapshots). Remaining: Chat 8 `assistant-extension/`, Chat 9 Compose.

## Slices

| Chat | File | Goal |
|------|------|------|
| 1 | [CHAT-01.md](impl/CHAT-01.md) | Schema + types + `AppSetting` |
| 2 | [CHAT-02.md](impl/CHAT-02.md) | Regex gate + browser job pages |
| 3 | [CHAT-03.md](impl/CHAT-03.md) | `Q_INDEX` v2 + verdict persistence |
| 4 | [CHAT-04.md](impl/CHAT-04.md) | Auth D7, `/ai/*`, phase-1 job-detail |
| 5 | [CHAT-05.md](impl/CHAT-05.md) | `ai-worker` call 1 |
| 6 | [CHAT-06.md](impl/CHAT-06.md) | Resume tailoring (call 2 + UI) |
| 7 | [CHAT-07.md](impl/CHAT-07.md) | Memory + `/assistant/*` + snapshots |
| 8 | [CHAT-08.md](impl/CHAT-08.md) | `assistant-extension/` Fill loop |
| 9 | [CHAT-09.md](impl/CHAT-09.md) | Compose (phase 5.5) |
