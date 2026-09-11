# API reference

The server exposes two families of endpoints: the **automation API** (consumed
by the extension) under `/decision/*`, and the **management/dashboard API**
under `/job/*` and `/report/*` (human + extension). All routes use the
`[Route("[controller]/[action]")]` convention, so the path is always
`/<controller>/<action>`.

Controllers: `Controllers/Decision.cs`, `Controllers/Job.cs`, `Controllers/Report.cs`.

## Authentication

Single-user, shared-secret auth (`Auth:ApiKey` in configuration). When the key
is configured, every endpoint requires it; without it (Development only) the
server runs auth-free and logs a warning.

- **Extension / API clients**: send header `X-API-Key: <key>` with every
  request. Missing/wrong key → `401` JSON (for `Accept: application/json`
  requests) or a redirect to the login page.
- **Dashboard / browser**: `GET /auth/login`, enter the same secret as the
  password → signed HttpOnly cookie (`js_auth`, DataProtection-protected,
  SameSite=Lax, 30 days). `GET /auth/logout` clears it.
- `/auth/*` and static files (`wwwroot`) are exempt from auth.
- Production startup **fails fast** when `Auth:ApiKey` / `Auth:CredentialKey`
  are missing; Development degrades with warnings.
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

## Management API — `JobController`

Human-facing job actions + an ad-hoc SQL console. Used by the dashboard.

### `GET /job/get/{jobid}`
Renders the `job-detail` view for one job.

### `POST /job/apply?jobid=`
Mark a job `Applied`.

### `POST /job/reject?jobid=`
Drop the job's HTML/content and mark it `Rejected`.

### `POST /job/options?jobid=` (body: serialized `ResumeContext`)
Replace the resume-keyword context (Options) computed for a job. Returns the
re-serialized context.

### `GET /job/resume?jobid=`
Render the tailored resume HTML for a job (uses stored Options).

### `GET /job/resume64?jobid=`
Same resume, returned as a downloadable `.html` file attachment.

### `POST /job/revaluate`
Kick off the background re-evaluation pass: re-score every job whose content is
available, against the current `JobOption` rules. Progress is surfaced via
`JobEligibilityHelper.CurrentRevaluationProcess`.

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
