# AI apply assistant

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 5). Records the design for
> a second browser extension — the *assistant* — that fills job-application
> forms on a personal terminal, driven by a local LLM and a learning memory
> stored on the core. No assistant code exists yet; `assistant-extension/`
> is a planned directory.

## 1. Stations: a fourth terminal, a second extension

The assistant adds a fourth station to the deployment topology
(AI_INTEGRATION.md §2): the search extension keeps scanning job boards on
the **search terminal**, while the assistant lives on the user's own
**personal terminal**, where apply forms are opened and filled.

**Never both extensions in one browser.** This is structural, not
preference: the search extension injects `check-page.js` on `*://*/*`
(`agent-extension/manifest.json`), so in a shared browser it would POST
every apply page the assistant works on to the core and execute the
returned `go`/`close` commands mid-fill. One extension per browser, one
terminal each.

The assistant connects outbound only:

- to the **core** (`X-API-Key` + `X-Client: assistant`): job list, applied
  reporting, memory CRUD;
- over the **LAN** to the AI station's `llama-server` (the core cannot
  proxy — Phase 0 rule — and the AI station never receives core
  credentials). `llama-server` stays localhost-bound through phase 3; the
  binding/proxy choice (and the home-LAN trust assumption it implies) is
  decided in phase 5 (AI_INTEGRATION.md §3, 2026-09-12).

Decided (2026-09-10): the assistant is **local-only** — it may carry
sensitive data, so hosted LLM endpoints are never used by the assistant,
and remote operation without LAN reachability to the AI station is out of
scope (no hosted fallback).

## 2. The decided flow (human-triggered)

```
user picks an Attention job in the assistant popup
   │  (list + resume text via GET /assistant/jobs)
   ▼
user reviews the resume, opens the job's apply page
   ▼
user presses Fill ──► assistant extracts the form inventory
   │                   (DOM controls: field_id, tag, type, label, options)
   ▼
agentic tool loop (llama-server over LAN):
   LLM receives: inventory + resume text + persona/rubric (system prompt)
   LLM returns:  memory_query / memory_write / fill(field_id, value)
   assistant executes the tools (core API / DOM) and posts results back
   ▼
user reviews, corrects fields, submits the form   ← the only submitter
   ▼
assistant diffs AI values vs final values → offers memory corrections
user tips in the chat → LLM writes memory rows
   ▼
user confirms → POST /assistant/applied → State = Applied
```

- **No new `JobState` value in v1.** Jobs stay `Attention` until the user
  confirms submission; abandoning a form writes nothing.
- **Multi-step forms** (Workday-style wizards): Fill acts on the currently
  visible step only; the inventory is re-extracted on every Fill press.
- **File-upload fields** are listed in the inventory but stay manual in v1
  (content scripts cannot set `input[type=file]`).

## 3. The tool loop and its guardrails

Tool set — exactly three tools, nothing else:

| Tool | Executed by | Against |
|------|-------------|---------|
| `memory_query(filter)` | assistant | core `/assistant/memory` |
| `memory_write(kind, domain, field, value, note)` | assistant | core `/assistant/memory` |
| `fill(field_id, value)` | assistant | page DOM (native setter + input/change events, cf. the `fill` case in `action-handler.js`) |

Guardrails (non-negotiable):

1. **Inventory, not raw HTML.** The model sees the extracted controls and
   visible instruction text only — no scripts, no CSS selectors. It
   references `field_id`s the extractor assigned; it can never emit
   selectors of its own (hallucination risk).
2. **No submit tool.** Page labels are site-controlled text (prompt
   injection); the human presses every submit button.
3. **Keys stay in the extension.** `llama-server` stays stateless and holds
   no core credentials; every core call is the extension's own.
4. **Trusted-context separation.** The job's resume text is user-authored
   and trusted; page text is data, never instructions.
5. **Structured tool transport.** OpenAI-compatible `tools` (Qwen3 needs
   `--jinja`); fallback: grammar-constrained JSON loop. Never regex-scrape
   model output.
6. **Context budget.** Fixed persona/rubric system prompt (prefix-cache
   friendly, AI_INTEGRATION.md §6) + resume text + inventory, capped at
   ~8k tokens.

## 4. Memory (the learning part)

Decided (2026-09-11): **one unified memory subsystem** — a single table, a
single API, and a single retrieval/injection mechanism shared by all three
lanes, with a `Scope` column distinguishing them (this replaces the earlier
separate `apply_memory` design):

| Column | Meaning |
|--------|---------|
| `Scope` | which lane the row serves: `resume` / `apply` / `ranking` |
| `AgencyDomain` | page hostname, or `'*'` for global |
| `FieldKey` | canonical field identity: prefer the control's `name` attribute, else normalized label text (lowercase, whitespace collapsed, trailing `:*` stripped); the raw label is stored beside it |
| `Kind` | `tip` (from chat) or `correction` (from submit-time diff) |
| `Value`, `Note` | the answer and free-text context |
| `UseCount`, `CreatedAt`, `UpdatedAt` | ranking and hygiene |

Precedence when several rows match one field: correction > tip; exact domain
> `'*'`; then highest `UseCount`; then newest `UpdatedAt`. `memory_query`
returns ranked matches, and the assistant bumps `UseCount` for rows whose
value it actually applied.

Two learning channels:

- **Auto diff capture (primary).** At submit time the assistant diffs
  AI-filled values against final human values (only fields the AI filled in
  that session) and offers them as corrections — no typing required.
- **Chat (complementary, assistant-mediated — decided 2026-09-11).** The
  side-panel chat transcript stays session-scoped
  (`chrome.storage.session`, never persisted to the core); both apply-form
  tips and resume-tailoring feedback reach the model here, and the model
  decides what to persist — instructing the assistant, as its agent, to
  write memory rows for injection into future prompts.

Privacy: rows hold PII, on the user-owned core, plaintext in v1
(documented); future hardening can reuse the existing AES-GCM
`CredentialKey` infrastructure. No memory-management dashboard in v1 — API
CRUD only. (Encryption decision deferred to the final phase — 2026-09-10.)

Decided (2026-09-11) — ranking feedback and hybrid injection:

- The ranking stage keeps a **separate raw override log** (`jobId`,
  `AiScore`, user action); durable lessons are distilled into shared memory
  rows (`Scope = ranking`) for injection into future ranking prompts
  ([`AI_INTEGRATION.md`](AI_INTEGRATION.md) §4.1) — no separate ranking
  memory table.
- **Hybrid injection:** high-confidence rows are deterministically
  pre-injected into prompts as a labeled data block (precedence:
  correction > tip; exact domain > `*`; then `UseCount`; then newest),
  token-capped at ~1–2k with stable ordering for prefix caching;
  `memory_query` handles exploratory retrieval. Memory is data, never
  instructions. `UseCount` bumps only when a row's value was actually
  applied; chat-driven writes are visible and deletable; page text never
  enters memory without user confirmation.

## 5. Personal data: resume text + memory

There is deliberately no structured profile table. Each Fill prompt carries
the job's resume as plain text — the resume already is the source of truth
for name, contacts, and history. The text is produced server-side
(`GET /assistant/jobs` renders `Options.HumanEdited ? Options :
(AiOptions ?? Options)` and strips tags with
HtmlAgilityPack, already a dependency); resume HTML never reaches the
extension. Facts outside the resume (salary expectation, tone, relocation)
arrive through chat and diffs and persist as memory. Human corrections
automatically become memory rows, so the system converges without a profile
form.

## 6. Core API surface (planned)

| Endpoint | Role gate | Purpose |
|----------|-----------|---------|
| `GET /assistant/jobs` | assistant | `Attention` jobs + per-job `resume_text` |
| `POST /assistant/applied {jobId}` | assistant | `State = Applied` (enum name as text) |
| `/assistant/memory` CRUD | assistant | learning memory |
| `GET /decision/scopes` | both roles | domain list (the assistant matches narrowly) |

`X-Client` role rules: `assistant` is rejected on `/decision/take`; `search`
is rejected on `/assistant/*` writes; `worker` (`ai-worker`) is restricted
to `/ai/*`; **absent header = legacy search** (current extension versions
send none); the search extension sends `X-Client: search` starting in
phase 1 (decided 2026-09-11 — one-line change, single deployment).

## 7. Out of scope v1

Automatic claim queue (would need an `Applying` state + lease),
deterministic pre-fill fast path from exact-label memory hits, file
uploads, structured profile table, memory-management dashboard UI, PII
encryption at rest, concurrent-session pausing, daily digest integration.
