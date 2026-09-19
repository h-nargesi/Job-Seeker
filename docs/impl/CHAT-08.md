# Chat 8 — `assistant-extension/` Fill loop

**Status:** not started. **Depends on:** Chat 7.

## Goal

Second MV3 extension on the AI-station browser. Generic DOM Fill + memory
UI + dual Applied. No Compose (Chat 9). No submit tool.

## Design pointers

[`AI_APPLY_ASSISTANT.md`](../AI_APPLY_ASSISTANT.md) §1–§3, §5, §7 (out of
scope). Topology: assistant browser on the AI station; `llama-server`
localhost; never both extensions in one browser.
[`agent-extension`](../../agent-extension/) is the **pattern** (vanilla JS,
no build, `tests/` with node:test + happy-dom), not a host to merge into.

## Files

- New `assistant-extension/` (`manifest.json`, popup, content script, tests).
  Do **not** inject `check-page.js` / `POST /decision/take`.
- Connect to core with Assistant key + `X-Client: assistant` (logs).
  `GET /decision/scopes` for narrow matching if needed.
- Page modes: default `job_detail` when tab origin = configured core URL,
  else `apply_form`; user may override. Job id from `/job/get/{id}` or
  `?jobid=`.
- Fill: extract inventory (`field_id`, tag, type, label, options) — **no**
  raw HTML/CSS selectors to the model. Tool loop **only**:
  `memory_query` / `memory_write` / `fill`. `fill` = native setter +
  input/change (same idea as `action-handler.js` fill). File inputs listed,
  left manual.
- Long/open textareas: resume facts or **confirmed** apply memory only;
  otherwise empty (no invented cover letters).
- Memory UI in the assistant: list / confirm / edit / delete / `memorycap`
  warning (overflow stored unused). Chat session in
  `chrome.storage.session` only. User-typed lessons: `Kind=tip`,
  `Confirmed=true`. On `job_detail` user must tag **ranking** or **delta**
  (`Scope` ranking vs resume). On `apply_form`, `Scope=apply` only.
  Fill-loop `memory_write` and submit-diff → unconfirmed.
- Submit-time: diff AI-filled vs final values → unconfirmed corrections.
  Human still clicks the site’s submit.
- Popup: Attention list, pending-proposal **warn** (Fill still allowed),
  mark Applied → `POST /assistant/applied`.
- Tests under `assistant-extension/tests/` (inventory, tools, mode
  defaulting, no submit tool). `npm test` analog of the search extension.

## Must not

Submit tool. Per-site adapters. Load this extension beside the search
extension. Invent long-form (Chat 9). Worker writes to memory.

## Validate

`dotnet build "Job Seeker.sln"` (unchanged unless core tweaks)
`cd assistant-extension && npm test`

Manual: load unpacked; confirm it never calls `/decision/take`.

## Done

Fill loop runs on whatever page is open; memory UI can confirm rows;
Applied is human-only on popup and/or job-detail.

## Starter prompt

```
Implement Chat 8 of the AI playbook (assistant-extension).

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md, docs/impl/CHAT-08.md,
docs/AI_APPLY_ASSISTANT.md (especially §1–§3 and guardrails),
agent-extension/ as a vanilla-JS MV3 pattern only.

Do only Chat 8. Stop at Done. Do not implement Compose (Chat 9).
Never add a submit tool. Never POST /decision/take from this extension.

Validate: cd assistant-extension && npm test
dotnet build "Job Seeker.sln"
```
