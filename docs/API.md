# API reference

The server exposes three families of endpoints: the **automation API** (consumed
by the search extension) under `/decision/*`, the **AI worker API** under
`/ai/*`, and the **management/dashboard API** under `/job/*` and `/report/*`.
All routes use the `[Route("[controller]/[action]")]` convention, so the path is
always `/<controller>/<action>`.

Controllers: `Controllers/Decision.cs`, `Controllers/Ai.cs`, `Controllers/Job.cs`,
`Controllers/Report.cs`.

## Authentication

Per-client keys (`Auth:ApiKeys:Dashboard`, `:Search`, `:Worker`). The matching
key **is** the role; `X-Client` is logged only and never used for allow/deny.

When the Dashboard key is configured, every non-exempt endpoint requires a
matching `X-API-Key` (or the dashboard cookie). Without a Dashboard key
(Development only) the server runs auth-free and logs a warning.

| Key | Path rules |
|-----|------------|
| `Auth:ApiKeys:Search` | `/decision/*` |
| `Auth:ApiKeys:Worker` | `/ai/*` |
| `Auth:ApiKeys:Dashboard` | everything |

- **Extension / worker**: send `X-API-Key: <that client's key>`. Wrong key or
  a key used on a disallowed path → `401` JSON (for `Accept: application/json`)
  or a redirect to the login page.
- **Dashboard / browser**: `GET /auth/login`, enter the **Dashboard** key as
  the password → signed HttpOnly cookie (`js_auth`, DataProtection-protected,
  SameSite=Lax, 30 days). `GET /auth/logout` clears it.
- `/auth/*` and static files (`wwwroot`) are exempt from auth.
- Production startup **fails fast** only when `Auth:ApiKeys:Dashboard` or
  `Auth:CredentialKey` is missing. Missing Search/Worker keys log a warning.
  Development degrades with warnings.
- Agency credentials (`Agency.Password`) are stored AES-GCM encrypted
  (`enc:` prefix) and migrated automatically on startup when
  `Auth:CredentialKey` (32-byte base64) is set.

## Automation API — `DecisionController`

These are what the Chrome extension calls.

### `POST /decision/take`
The heart of the loop. The extension posts the current page's full HTML; the
server analyzes it and returns the next browser commands. Request body is
capped at 5 MB (`[RequestSizeLimit]`).

- **Request body** (`PageContext`):
  ```json
  { "trend": 123, "agency": "LinkedIn", "url": "https://...", "content": "<full HTML>" }
  ```
  - `trend` — optional; the workflow id bound to this tab (stamped by the
    extension from the previous response). Omit for the first request.
  - `agency` — must match an `Agency.Name` / DB `Title`.
  - `content` — `document.documentElement.outerHTML` from the extension.
- **Response**:
  ```json
  {
    "trend": 124,
    "commands": [ { "action": "go", "params": { "url": "..." } } ],
    "close_timeout_ms": 90000
  }
  ```
  `commands` is an array of `Command` (see [Commands](#commands)).
  `close_timeout_ms` — the tab auto-close timeout the extension should arm
  for this tab: `90000` (90 s) by default, `600000` (10 min) while the trend
  state is `Auth` (human 2FA/login wait).
- Returns `400` on a `BadJobRequest` (unknown agency, empty url/content).

### `POST /decision/heartbeat`
Keeps a tab's trend row alive during human pauses (2FA/CAPTCHA). The
extension's content script calls it every 30 s while its tab is visible; no
HTML analysis, no state mutation — just refreshes `LastActivity`.

- **Request body** (`HeartbeatContext`):
  ```json
  { "trend": 123 }
  ```
  `trend` — the tab's bound trend id; may be `null` when the tab has no
  binding yet (nothing is touched).
- **Response**:
  ```json
  { "trend": 123 }
  ```
  `{ "trend": null }` when the id is unknown/expired — the tab's next `take`
  re-adopts or regenerates the workflow.

### `GET /decision/scopes`
Returns the active agencies the extension should react to. Cached on the client
(the extension re-fetches when its cache is older than ~60 s; there is no
server-side invalidation).

- **Response**: array of `{ "name", "domain", "waiting" }`.
  - `domain` — regex; the extension matches `window.location.hostname` against it.
  - `waiting` — optional delay (ms) the extension honors before sending the page.

> Correction: earlier docs described `POST /decision/scopes?reset=true` — that
> endpoint never existed server-side. The "reset" was the dashboard's content
> script clearing its own cache; cache refresh is now the client-side TTL.

### `GET /decision/orders`
The polling path. Asks "what should open next?" with no page HTML. Polled by
the extension's **service worker** on a ~30 s `chrome.alarms` timer, gated by
the popup's Trend Ordering toggle — the dashboard does not poll it. Runs
`TrendsCheckpoint.CheckCurrentTrends()` and returns commands to start idle
trends (e.g. open the search page).

- **Response**: `{ trendID, agencyID, state, commands }`. The service worker
  consumes `commands` only — on this path only `open` is ever emitted (the
  poller skips the trailing `close`). `CheckCurrentTrends` also sweeps expired
  trends and reservations older than 30 s, so each poll is the fast re-open
  path for abandoned `Open`s.

### `POST /decision/reset`
Wipes all trend rows instantly — expired and reserved alike
(`DeleteExpired(0)` + `DeleteExpiredReservations(0)`) — and clears the
in-memory agency cache so it reloads from the DB on next access.

### `POST /decision/running`
Start/stop an agency's active seeking, or set its current locale index.

- **Request body** (`RunningMethodContext`): `{ "agency": "LinkedIn", "running": 2 }`.
  - `running` present → set `CurrentMethodIndex`, enable `ActiveSeeking`, clear
    that agency's search trends.
  - `running` null/omitted → disable `ActiveSeeking` for the agency.

## AI worker API — `AiController`

Worker-only (`Auth:ApiKeys:Worker`). Payloads are data, not finished prompts.

### `GET /ai/next`
Read-only. Oldest `AiPending` job with `Content`. Empty queue → `200` `{ "empty": true }`.

- **Response** (when a job exists): `{ empty: false, jobId, content, resume, keywords: [{ category, score, title }], fingerprint, settings: { aipassmark }, options, inventory: [{ id, type, keys, text }] }`.
- `keywords` comes from cached `JobOption.FetchAll` order with the `reject` category omitted.
- `fingerprint` is SHA-256 hex of whitespace-normalized `Content` (not stored).
- `resume` is the pruned/stripped master resume (16k-token cap from the top).
- `options` is the job's current `ResumeContext` as standard JSON (never the
  dashboard's simple-JSON exchange format).
- `inventory` is the template-derived block inventory (cached in memory like the
  master resume): `id` = selector (`title`, `summary`, `#article`, `#article
  li:nth-child(n)`, `.key-*`), `type` = `slot`/`block`/`bullet`/`key`, `keys` =
  the block's `key-*` classes, `text` = full template text on editable slots
  (`slot`, `bullet`), ≤ 120-char excerpt elsewhere.

### `POST /ai/verdict?jobid=`
Idempotent upsert. Body is the call-1 JSON (snake_case extraction fields). An absent `delta` is ignored.

- **Required**: `relevance` (0–100), `verdict` (AiVerdict name), `fingerprint`.
- **Optional**: `reason` (≤ 2000), `skills` (≤ 20), `seniority` / `period` / `work_model` / `contract` (enum names), `salary_min` / `salary_max` (≥ 0), `experience_years` (0–50), `currency`, `delta`.
- `delta` (call-2 tailoring, validated by the core independently of the verdict):
  `{ keys: [...], included: [...], notIncluded: [...], length: 1|2, texts: { slot: "..." } }`.
  - Applied only when the verdict promotes the job to `Attention` (queued,
    fingerprint match, non-`Error`, score ≥ passmark).
  - Selection half: `keys` ⊆ `MainKeys` (≤ 8), selectors ⊆ inventory whitelist
    (≤ 30 total), `length` ∈ {1, 2}. Applied onto the regex-built `Options` and
    stored complete in `AiOptions` (live unless `Options.HumanEdited`).
  - Texts half: slots ⊆ `title`/`summary`/inventory bullets, no HTML, `title`
    one line ≤ 80 chars. Writes `ResumeText.proposal` (`pending`); never
    touches `live`. A rejected slot is not re-proposed with identical wording.
  - The halves validate independently; an invalid half is dropped (with the
    reason in `job.Log`), never a 400. The raw delta is appended to `job.Log`.
- **404** if the job is gone; **400** `{ error: "validation", message }` if the payload is rejected.

## Management API — `JobController`

Human-facing job actions + an ad-hoc SQL console. Used by the dashboard.

### `GET /job/get/{jobid}`
Renders the `job-detail` view for one job (AI badges, extraction, re-queue / force revaluate / emergency promote).

### `POST /job/apply?jobid=`
Mark a job `Applied`.

### `POST /job/reject?jobid=`
Drop the job's HTML/content and mark it `Rejected`.

### `POST /job/options?jobid=` (body: serialized `ResumeContext`)
Replace the resume-keyword context (Options) computed for a job — marks it
`HumanEdited`, so later AI selections stay diff-only until accepted. Returns
the re-serialized context. Selection only — text overlays live in `ResumeText`.

### `POST /job/acceptai?jobid=`
Accept the AI selection: full copy of `AiOptions` into `Options` (sets
`HumanEdited`). `400` when there is no `AiOptions`.

### `POST /job/resumetext?jobid=&slot=&op=` (op `live`: body = raw string)
Per-slot `ResumeText` operations on job-detail:

- `op=accept` — copy `proposal` → `live` (status `accepted`); does not set
  `HumanEdited` (text accept is not a selection accept).
- `op=reject` — mark the proposal `rejected`; `live` and the template default
  are untouched.
- `op=live` — write the body string as `live` (status `accepted`, human
  source); empty/null clears `live` back to the template default.

`slot` must be `title`, `summary`, or an existing `ResumeText` key; otherwise
`400`.

### `GET /job/resume?jobid=`
Render the tailored resume HTML for a job (selection precedence
`Options.HumanEdited ? Options : (AiOptions ?? Options)`, then a `ResumeText.live`
overlay via the template's client JS; proposals never render).

### `GET /job/resume64?jobid=`
Same resume, returned as a downloadable `.html` file attachment.

### `POST /job/revaluate`
With no query: kick off the background re-evaluation pass. Progress is surfaced
via `JobEligibilityHelper.CurrentRevaluationProcess`.

With `?jobid=`: force re-score that one job (ignores `HumanEdited`; keeps the
`Rejected`/`Applied` guard; hidden/400 when `Content` is null). Clears
`AiOptions` and `ResumeText.proposal` (`live` survives).

### `POST /job/requeue?jobid=`
`AIError` / `NotApprovedAI` → `AiPending` when `Content` is present; otherwise 400.

### `POST /job/promote?jobid=`
Emergency promote: `AiPending` / `NotApprovedAI` / `AIError` → `Attention`.

### `POST /job/clean`
Run the retention queries (`JobBusiness.Clean`): delete old jobs (keeping
`Applied`), trim HTML for old `Attention`/`NotApproved` jobs. Pass
`?vacuum=true` to also `VACUUM` (rewrites the whole DB file — slow).

### `GET /job/options`
Render the `job-options` view (the scoring-rules editor).

### `POST /job/setting` (body: `{ "type": "E"|"Q", "query": "<sql>" }`)
Ad-hoc SQL console.
- `type:"reload"` → `analyzer.ReloadSettings()` (re-read agency config).
- `type:"E"` → execute `query`.
- `type:"Q"` → run `query` and return rows.

## Dashboard — `ReportController`

Razor-view pages (HTML), not JSON.

| Route | View | Content |
|-------|------|---------|
| `GET /` | `index` | Combined overview (trends + jobs + agencies) |
| `GET /report/trends` | `trends` | Active/expiring workflow trends |
| `GET /report/jobs?agencies=&countries=` | `jobs` | Ranked job list, filterable |
| `GET /report/agencies` | `agencies` | Per-platform stats (counts, accept rates) |

## Commands

The `Command` struct (`Result/Command.cs`) → serialized JSON consumed by
`action-handler.js`. `action` is the lowercase `PageAction` enum name.

| `action` | params / object | Extension behavior |
|----------|-----------------|--------------------|
| `go` | `params.url` | `window.location = url` (navigate current tab) |
| `open` | `params.url` | New background tab via the service worker (`chrome.tabs.create`) |
| `fill` | `object` = selector, `params.value` | Set `value`/`innerText` on matched elements |
| `click` | `object` = selector | `.click()` matched elements |
| `recheck` | — | Re-run the page-load handler in-place (no navigation) |
| `close` | — | Close the current tab via the service worker (`chrome.tabs.remove`; skipped on the `/orders` polling path) |
| `wait` | `params.miliseconds` | `setTimeout` delay |
| `reload` | — | `location.reload()` |

Factory helpers exist for all of them: `Command.Go/Open/Fill/Click/Recheck/Reload/Close/Wait`.

## Error handling convention

Controllers catch exceptions, log via Serilog (`Log.Error`), and either return
`BadRequest()` (for `BadJobRequest`) or rethrow (500). Scoring/analysis failures
are non-fatal to the loop — they surface as logs, not crashes.

Unhandled exceptions are caught by a global handler (`UseExceptionHandler`)
that logs and returns a bare 500 without stack-trace details. The detailed
developer error page remains active only in Development (the ASP.NET Core
default).
