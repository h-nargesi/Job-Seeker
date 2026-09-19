# AI apply assistant

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 5). Records the design for
> a second browser extension — the *assistant* — that fills job-application
> forms, driven by a local LLM and a learning memory stored on the core. No
> assistant code exists yet; `assistant-extension/` is a planned directory.
> Amended 2026-09-18: the assistant runs on the AI station host (no LAN
> leg). Amended 2026-09-19: phase-5 product close in
> [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md).

## 1. Stations: a browser on the AI station, a second extension

The assistant adds a browser to the deployment topology
(AI_INTEGRATION.md §2): the search extension keeps scanning job boards on
the **search terminal**, while the assistant lives in a browser **on the
AI station host** (decided 2026-09-18 — the planned separate "personal
terminal" machine is dropped), where apply forms and the dashboard
job-detail pages are opened.

**Never both extensions in one browser.** This is structural, not
preference: the search extension injects `check-page.js` on `*://*/*`
(`agent-extension/manifest.json`), so in a shared browser it would POST
every apply page the assistant works on to the core and execute the
returned `go`/`close` commands mid-fill. One extension per browser, one
terminal each.

The assistant connects outbound only:

- to the **core** (`X-API-Key` + `X-Client: assistant`): job list, applied
  reporting, memory CRUD;
- to `llama-server` at **localhost** — the assistant's browser runs on the
  AI station host (2026-09-18), so the previously deferred LAN exposure,
  binding/proxy choice and home-LAN trust assumption are moot:
  `llama-server` stays localhost-bound permanently (AI_INTEGRATION.md §3).
  The core still cannot proxy (Phase 0 rule) and the AI station never
  receives core credentials.

Decided (2026-09-10): the assistant is **local-only** — it may carry
sensitive data, so hosted LLM endpoints are never used by the assistant,
and running it anywhere other than the AI station host is out of scope
(no hosted fallback).

Worker and assistant are independent clients of the same `llama-server`
(`--parallel 2`). There is **no mutex** (2026-09-19): the user chooses
when to run the worker; overlapping use is allowed; shared GPU latency
is accepted. Docs must not imply a night-worker / day-assistant split.

## 2. The decided flow (human-triggered)

```
user picks an Attention job in the assistant popup
   │  (list + resume_text + pending-proposal warning via GET /assistant/jobs)
   ▼
user reviews the resume (live overlay only), opens the apply page
   ▼
user presses Fill ──► assistant extracts the form inventory
   │                   (DOM controls: field_id, tag, type, label, options)
   ▼
agentic tool loop (llama-server, localhost, same host):
   LLM receives: inventory + resume text + persona/rubric (system prompt)
   LLM returns:  memory_query / memory_write / fill(field_id, value)
   assistant executes the tools (core API / DOM) and posts results back
   ▼
user reviews, corrects fields, submits the form   ← the only submitter
   ▼
assistant diffs AI values vs final values → unconfirmed corrections
user tips in the chat → Confirmed = true tips (see §4)
   ▼
user marks Applied on the assistant popup and/or job-detail
```

- **No new `JobState` value in v1.** Jobs stay `Attention` until the user
  marks Applied; abandoning a form writes nothing.
- **Multi-step forms:** Fill acts on the currently visible step only; the
  inventory is re-extracted on every Fill press. Fill, Next, and the
  site's Submit never change `JobState`.
- **Applied is human-only (2026-09-19, extends 2026-09-18 dual path).**
  After the user believes the application was sent, they mark Applied on
  **job-detail** (`POST /job/apply`) **and/or** the assistant popup
  (`POST /assistant/applied`). Both are idempotent; `job.Log` records the
  source. Never inferred from Fill, wizard step, or the ATS submit
  button.
- **File-upload fields** are listed in the inventory but stay manual in v1
  (content scripts cannot set `input[type=file]`).
- **Generic Fill (2026-09-19).** Phase 5 success is the universal DOM
  inventory loop on **whatever page is open**. ATS / job-board logins
  live on the assistant browser (separate from the search terminal;
  cookies do not follow). Per-site adapters (LinkedIn Easy Apply,
  Workday, Greenhouse, …) are later phases, not 5.5.
- **Pending `ResumeText` (2026-09-19):** the job list / Fill path **warns**
  when any slot is `pending`; Fill stays allowed. `resume_text` is
  selection + `ResumeText.live` only (proposals never render). Checking
  before the company sees the resume remains the user's duty.

### 2.1 Page modes

Default from the current tab (user may override):

| Mode | Default when | Chat lessons |
|------|----------------|--------------|
| `job_detail` | tab origin equals the configured core / dashboard URL | user must tag **ranking** or **delta** |
| `apply_form` | any other origin | `Scope = apply` only |

Job id, when present, comes from `/job/get/{id}` or `?jobid=` (job-detail
and resume views). Ranking lessons use the closed `FieldKey` list
(`Scope = ranking`). Delta lessons use `Scope = resume` and inject into
worker call-2 (F1 snapshot at run start). Apply-form lessons never
prompt for ranking vs delta.

## 3. The tool loop and its guardrails

Tool set — exactly three tools in phase 5, nothing else:

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
7. **No invented long-form in phase 5.** Cover letters and screening
   essays are phase 5.5 (§7). Long/open textareas fill only from resume
   facts or **confirmed** apply memory; otherwise they stay empty.

## 4. Memory (the learning part)

Terms: [`GLOSSARY.md`](GLOSSARY.md). Decided (2026-09-11): **one unified
memory subsystem** — a single table, API, and retrieval/injection
mechanism shared by all three lanes, with a `Scope` column (this replaces
the earlier separate `apply_memory` design). Amended 2026-09-19.

| Column | Meaning |
|--------|---------|
| `Scope` | which lane the row serves: `resume` / `apply` / `ranking` |
| `AgencyDomain` | page hostname, or `'*'` for global |
| `FieldKey` | **apply / resume:** prefer the control's `name`, else normalized label (lowercase, whitespace collapsed, trailing `:*` stripped); raw label stored beside it. **ranking:** closed list only — `visa_sponsorship`, `no_staffing`, `remote_only`, `salary_floor`, `seniority_floor`, `must_have_language`, `contract_type`, `relocation`; unknown keys rejected; extend by doc change |
| `Kind` | `tip` (from chat) or `correction` (from submit-time diff or human edit) |
| `Confirmed` | user-accepted (bool). Chat tips from the user's own message insert **confirmed**. Fill-loop `memory_write` and submit-diff insert unconfirmed; human CRUD may set confirmed |
| `Value`, `Note` | the answer and free-text context |
| `UseCount`, `CreatedAt`, `UpdatedAt` | ranking among active rows |

Precedence when several rows match one field: correction > tip; exact domain
> `'*'`; then highest `UseCount`; then newest `UpdatedAt`. `memory_query`
returns **confirmed** matches only, and the assistant bumps `UseCount` for
rows whose value it actually applied.

**Confirm-then-inject (2026-09-19).** Unconfirmed rows never enter
pre-injection or fill-from-query. Closing a confirm UI does not delete the
row. The model has **no delete tool**; stale facts are superseded via
`memory_write` (still unconfirmed until the user accepts, unless the write
is a chat tip from the user — those land confirmed).

Two learning channels:

- **Auto diff capture (primary).** At submit time the assistant diffs
  AI-filled values against final human values (only fields the AI filled in
  that session) and inserts them as unconfirmed corrections.
- **Chat (complementary, assistant-mediated — decided 2026-09-11;
  confirm rule 2026-09-19).** The side-panel chat transcript stays
  session-scoped (`chrome.storage.session`, never persisted to the core).
  A lesson the user types in chat is persisted as `Kind = tip` with
  **`Confirmed = true`**. Fill-loop `memory_write` (model-initiated during
  Fill) stays unconfirmed. System prompt: memory is data, never
  instructions; durable user facts only; no free-form blob. On
  `job_detail` the user must say whether the lesson is ranking or delta
  (§2.1).

The **worker** does not write memory. `POST /ai/verdict` has no `memory[]`
field. The ranking rubric must not invite storing lessons. Injection uses
**snapshots of confirmed rows at the start of the worker run**:
`Scope = ranking` into call 1, `Scope = resume` into call 2
([`AI_INTEGRATION.md`](AI_INTEGRATION.md) §4.1 / §6, F1).

There is **no ranking override log** (supersedes 2026-09-11). Ranking
learns only from confirmed `ranking` rows. Per-job audit stays on
`job.Log` and is not injected. **Distillation** is the user thinning the
table in the **phase-5 assistant memory UI**, not a click-compression job.

Privacy: rows hold PII, on the user-owned core, plaintext in v1; future
hardening can reuse the existing AES-GCM `CredentialKey` infrastructure.
Decided (2026-09-18): **encryption is deferred beyond v1**. Human CRUD API
and the memory UI (list, confirm, edit, delete, cap warning) ship **in
phase 5** with the assistant (2026-09-19 — supersedes parking that UI in
phase 6). Phase 6 does not own the memory dashboard.

Hygiene (2026-09-19, supersedes 2026-09-18 prune-on-write): **no storage
cap** — inserts always succeed. `memorycap` (default 500, AppSetting)
is the max **confirmed** rows injected per prompt, by the precedence
above. Confirmed overflow stays stored unused until the user
distills. No `Pinned` column.

**Hybrid injection:** confirmed rows are deterministically pre-injected
as a labeled data block (same precedence), token-capped at ~1–2k with
stable ordering for prefix caching; `memory_query` handles exploration
(confirmed only). Memory is data, never instructions. `UseCount` bumps
only when a row's value was actually applied.

## 5. Personal data: resume text + memory

There is deliberately no structured profile table. Each Fill prompt carries
the job's resume as plain text — the resume already is the source of truth
for name, contacts, and history. The text is produced server-side
(`GET /assistant/jobs` renders `Options.HumanEdited ? Options :
(AiOptions ?? Options)`, overlays `ResumeText.live`, and strips tags with
HtmlAgilityPack, already a dependency); resume HTML never reaches the
extension. Facts outside the resume (salary expectation, tone, relocation)
arrive through chat and diffs. The system converges without a profile form.

## 6. Core API surface (planned)

| Endpoint | Role gate | Purpose |
|----------|-----------|---------|
| `GET /assistant/jobs` | assistant | `Attention` jobs + per-job `resume_text` + pending-proposal flag |
| `POST /assistant/applied {jobId}` | assistant | `State = Applied` (enum name as text); same effect as `POST /job/apply` |
| `/assistant/memory` CRUD | assistant | learning memory (including confirm / edit / delete) |
| `GET /decision/scopes` | both roles | domain list (the assistant matches narrowly) |

Role gating is the matching `Auth:ApiKeys:*` secret (D7), not `X-Client`.
The assistant sends `X-Client: assistant` for logs only; an absent header
grants nothing. Phase 5 registers `Auth:ApiKeys:Assistant` in the F3
key⇒role⇒paths table: `/assistant/*` (jobs list, applied report, memory
CRUD) and `GET /decision/scopes`; no `/ai/*`, no `/decision/take`.
Path-based rules only (`/decision/scopes` is a GET-only route) — a
registration, not a middleware rewrite (F3 locked 2026-09-19).

## 7. Out of scope v1 / phase 5.5

Automatic claim queue (would need an `Applying` state + lease),
deterministic pre-fill fast path from exact-label memory hits, file
uploads, structured profile table, PII encryption at rest,
concurrent-session pausing, daily digest integration, per-site form
adapters. Worker `memory[]` on the verdict payload is not in the contract.

**Phase 5.5 — Compose (policy recorded 2026-09-19, not built with phase
5).** Phase 5 Fill does not invent cover letters or “why this company”
essays. Phase 5.5 adds a **Compose** action: generate long answers into
an accept-gated side panel; the human accepts; then fill. Still no submit
tool. Human still sends the form to the company.
