# API reference

The server exposes two families of endpoints: the **automation API** (consumed
by the extension) under `/decision/*`, and the **management/dashboard API**
under `/job/*` and `/report/*` (human + extension). All routes use the
`[Route("[controller]/[action]")]` convention, so the path is always
`/<controller>/<action>`.

Controllers: `Controllers/Decision.cs`, `Controllers/Job.cs`, `Controllers/Report.cs`.

## Automation API — `DecisionController`

These are what the Chrome extension calls.

### `POST /decision/take`
The heart of the loop. The extension posts the current page's full HTML; the
server analyzes it and returns the next browser commands.

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
  { "trend": 124, "commands": [ { "action": "go", "params": { "url": "..." } } ] }
  ```
  `commands` is an array of `Command` (see [Commands](#commands)).
- Returns `400` on a `BadJobRequest` (unknown agency, empty url/content).

### `GET /decision/scopes`
Returns the active agencies the extension should react to. Cached on the client
until a reset.

- **Response**: array of `{ "name", "domain", "waiting" }`.
  - `domain` — regex; the extension matches `window.location.hostname` against it.
  - `waiting` — optional delay (ms) the extension honors before sending the page.

### `POST /decision/scopes?reset=true`
(POST overload) Invalidates the extension's cached scope list. Triggered from
the dashboard's "reset" button.

### `GET /decision/orders`
The polling path. Asks "what should open next?" with no page HTML. Runs
`TrendsCheckpoint.CheckCurrentTrends()` and returns commands to start idle
trends (e.g. open the search page, go to the next saved job).

- **Response**: same shape as `take` (`{ trend, commands }`).

### `POST /decision/reset`
Clears expired trends (`DeleteExpired(0)`) and clears the in-memory agency cache
so it reloads from the DB on next access.

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
`Applied`), trim HTML for old `Attention`/`NotApproved` jobs, `VACUUM`.

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
| `open` | `params.url` | `window.open(url)` (new tab/window) |
| `fill` | `object` = selector, `params.value` | Set `value`/`innerText` on matched elements |
| `click` | `object` = selector | `.click()` matched elements |
| `recheck` | — | Re-run the page-load handler in-place (no navigation) |
| `close` | — | Close the current tab (skipped on the `/orders` polling path) |
| `wait` | `params.miliseconds` | `setTimeout` delay |
| `reload` | — | `location.reload()` |

Factory helpers exist for all of them: `Command.Go/Open/Fill/Click/Recheck/Reload/Close/Wait`.

## Error handling convention

Controllers catch exceptions, log via Serilog (`Log.Error`), and either return
`BadRequest()` (for `BadJobRequest`) or rethrow (500). Scoring/analysis failures
are non-fatal to the loop — they surface as logs, not crashes.
