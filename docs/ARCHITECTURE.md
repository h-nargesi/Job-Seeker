# Architecture

This document explains how the three components (server, extension, database)
fit together and where the core logic lives. Read this before changing the
scraping or workflow logic.

## Components at a glance

| Component | Tech | Role |
|-----------|------|------|
| `core-decision-dotnet/` | ASP.NET Core 8 (`Photon.JobSeeker`) | Scrapes HTML, scores/ranks jobs, decides the browser's next actions, serves the dashboard |
| `agent-extension/` | Chrome MV3, vanilla JS | Drives the browser: captures page HTML, sends it to the server, executes returned commands |
| `database/` | SQLite (raw, no EF Core) | Persists jobs, trends (workflow state), agencies, and scoring options |

The server is the brain; the extension is the hands. The server never touches a
browser — it only reads HTML and emits `Command[]`.

## The request loop (extension ↔ server)

The whole system is a poll loop. There is no WebSocket; each navigation triggers
one HTTP round-trip.

```
┌─────────────────────┐                         ┌──────────────────────────┐
│  Chrome extension   │                         │  .NET server             │
│  (agent-extension)  │                         │  (core-decision-dotnet)  │
└──────────┬──────────┘                         └────────────┬─────────────┘
           │ page loads                                        │
           ▼                                                   │
  check-page.js                                                │
  fetch /decision/scopes  ─────────────────────────►  GET Scopes()
  (agency list + domain regexes) ◄───────────────────  return [{Name,Domain,Waiting}]
           │                                                   │
  match window.location.hostname against a Domain            │
           │ matched                                          │
           ▼                                                   │
  POST /decision/take  ──────────────────────────────►  POST Take(PageContext)
    { agency, url, content: fullHTML }                       │
                                                             ▼
                                               Analyzer.Analyze(context)
                                               ├─ Agency.AnalyzeContent(url, content)
                                               │    └─ first Page with IssueCommand() != null
                                               ├─ TrendsCheckpoint.CheckCurrentTrends()
                                               │    (updates/creates workflow trends,
                                               │     injects open/go/close commands)
                                               └─ result: { TrendID, State, Commands[] }
           │ ◄────────────────────────────────  200 { trend, commands[] }
           ▼
  action-handler.js runs each Command sequentially
  (go → navigate, click → DOM, fill → input, close → tab, ...)
           │
  page changes (or recheck fires) → loop repeats
```

Two entry points trigger analysis:

1. **Page load** (`check-page.js` → `SendingPageInfo`): the normal reactive path.
   Fires whenever the browser navigates to a matched domain.
2. **Orders polling** (service worker `background.js` → `CheckNewOrders`): the
   extension's service worker polls `GET /decision/orders` on a 30 s
   `chrome.alarms` timer, gated by the popup's "Trend Ordering" toggle, to ask
   "what should I open next?". This is how idle trends (e.g. "open the search
   page") get acted on even when no user-triggered navigation is happening.
   The dashboard is a human control/monitor console only — no extension code
   runs on it.

## Server internals

### `Analyzer` (singleton, DI-registered)

- Holds two dictionaries: `Agencies` (by `Name`, only **active** ones) and
  `AgenciesByID` (all loaded, including inactive).
- Lazy-loads agencies on first access via `LoadAgencies()`:
  `TypeHelper.GetSubTypes(typeof(Agency))` reflects over the assembly, so a new
  platform class is discovered automatically — **no registration list**.
  Each agency's `Status` bitfield decides whether it is included in `Agencies`.
- `Analyze(context)` → `AnalyzeContent` (finds the agency, runs its pages) →
  hands the `Result` to `TrendsCheckpoint` for state bookkeeping.
- `ReloadSettings()` re-reads agency config from the DB without a restart
  (invoked from `JobController.Setting` with `Query == "reload"`).

### `Agency` (abstract base, one subclass per platform)

Each platform (LinkedIn, Indeed, Bayt, Stepstone, IamExpat, Glassdoor) is a
class under `Analyze/<Platform>/`. Key members:

- `Name` — must match the `Agency.Title` row in the DB.
- `Domain` — regex loaded from DB, used by the extension to match hostnames.
- `SearchLink` — the search URL the browser is sent to.
- `JobAcceptabilityChecker` — regex that detects expired jobs ("No longer
  accepting applications"); gating for scoring.
- `CurrentMethod` / `SearchingMethodCount` — a platform can search multiple
  locales/countries (a "searching method" = one locale). `Settings.methods[]`
  in `agency.sql` defines them; `Running` tracks the current index. When a
  method is exhausted, the agency auto-advances to the next, then flips
  `ActiveSeeking` off when all are done.
- `GetSubPages()` — returns that platform's `Page` subclasses (also reflection
  discovered). Pages are sorted by `Order`.

`AnalyzeContent(url, content)` walks its pages in `Order`; the **first** page
whose `IssueCommand` returns non-null wins and its commands become the result.

Concurrency: `AnalyzeContent`, `ApplyRunning` and `LoadSettings` run under a
per-instance lock (`agency_lock`) — analyses of one agency are serialized while
different agencies stay parallel. Lock-order rule: the agency lock is
leaf-level; never hold it while entering `TrendsCheckpoint.CheckCurrentTrends`.
The only allowed nesting is Analyzer-wide lock → agency lock (the
`ReloadSettings` path); future checkpoint-driven intent application (REVIEW
3.26) must keep the direction `checkpoint_lock` → agency lock.

### Pages (`Analyze/Pages/`)

Abstract roles, each with an `Order` (lower = checked first):

| Page (abstract) | Order | `TrendState` | Responsibility |
|-----------------|-------|--------------|----------------|
| `LoginPage` | 1 | `Login` | Detect login form, fill credentials, submit |
| `AuthPage` | 2 | `Auth` | Handle auth/consent interstitials, redirect to login |
| `JobPage` | 10 | `Analyzing` | Load a single job, extract title/code/apply link, run scoring |
| `SearchPage` | 20 | `Seeking` | Validate search URL/filters, extract job links, save them, click next page |
| `OtherPages` | 100 | `Other` | Catch-all for unsupported states (returns no commands) |

A platform implements concrete subclasses (e.g. `LinkedInPageSearch`) that
override abstract methods like `GetJobUrls`, `CheckNextButton`, `GetJobContent`.
The shared regexes usually live in a per-platform `<Platform>Page` interface
(e.g. `LinkedInPage.cs`).

**The search→job handoff:** `SearchPage.IssueCommand` saves each discovered job
URL with `State = Saved`. Later, `TrendsCheckpoint` (via `JobBusiness.GetFirstJob`)
picks the next `Saved` job and emits a `Command.Go(jobUrl)` to start analyzing
it.

### `TrendsCheckpoint` (scoped, the workflow state machine)

A *trend* = one in-flight workflow step for one agency, persisted in the
`Trend` table (`AgencyID` + `Type` is unique). Trend `Type` ∈
{Blocked, Login, Search, Job}. The checkpoint reconciles the just-analyzed
result against the DB trends and decides:

- update the matched trend's `LastActivity`/`State`,
- reserve/create new trends for work that should start (then inject `Command.Open`
  for the search page, or `Command.Go` for the next job),
- `Block` (close) trends whose agency is no longer active,
- ensure a `Command.Close` is appended when there is nothing actionable.

See the truth table in `Analyze/TrendsCheckpoint.md` (columns AT = matched
analyzed result, DT = had no trend id, RT = existing DB trend).

### `JobEligibilityHelper` (the scoring engine)

Detailed in [`SCORING.md`](SCORING.md). In short: it strips HTML to text, checks
the job is English enough (≥50%), runs every `JobOption` regex against the
content, accumulates a score per category with diminishing returns (first match
full score, then 0.5× for duplicates in the same key), and sets the job's
`State`. Jobs below `MinEligibilityScore` (100) or rejected have their
`Html`/`Content` purged to save space.

### Database layer (`Database/`)

- `Database.cs` — Dapper wrapper over one `SQLiteConnection`; instances come
  from `IDatabaseFactory`/`DatabaseFactory` (singleton in DI, also runs the WAL
  and busy-timeout PRAGMAs) — scoped `Database` for controllers, factory
  openings for the singleton `Analyzer`/pages.
- `Dictionaries.cs` — separate read-only `dictionaries.sqlite3` (English words)
  for language detection.
- `Business/<Entity>Business.cs` — per-entity repos (`Job`, `Agency`, `Trend`,
  `JobOption`). `BaseBusiness<T>.Save` upserts: if the model id is 0 it inserts
  (with an `ON CONFLICT ... DO NOTHING` guard), else updates by id.

## Extension internals

All logs are prefixed `console.log("AGENT", ...)`.

| File | Role |
|------|------|
| `manifest.json` | MV3 manifest; content scripts on `*://*/*`, service worker `background.js` |
| `controllers/check-page.js` | Runs on every page load. Matches hostname → agency, posts HTML to `/decision/take`. Inert on the dashboard (server-origin / `#job-seeker-trend-list` check). |
| `controllers/background.js` | Service worker. Routes messages, stamps the `trend` id onto each request, maps responses back to tabs, and drives idle trends: polls `/decision/orders` on a 30 s alarm and executes `open` commands (gated by the popup's ordering toggle). |
| `controllers/core-messaging.js` | Thin HTTP client for the server endpoints (`take`, `scopes`, `orders`, `heartbeat`); sends `X-Client: search`. |
| `controllers/action-handler.js` | Executes `Command[]`. Maps `go/open/fill/click/recheck/close/wait/reload` to DOM/window calls. |
| `controllers/trend-collection.js` | In-memory map `windowId → tabId → trendId`. |
| `controllers/storage-handler.js` | Persists settings (server URL, API key, ordering flag, last-poll status) in `chrome.storage.local`. |
| `application/menu.html` + `menu.js` | Popup UI: server URL, API key, Trend Ordering toggle, last-poll status. |

The `recheck` command is special: it re-runs `OnPageLoad` in-page (no real
navigation) so the server can re-evaluate after, e.g., clicking "next page".

## Configuration

- `appsettings.json` → `Database:Path` (`../database/data.sqlite3`),
  `Database:Dictionaries` (`../database/dictionaries.sqlite3`),
  `Logging:FilePath` (`logs/E.log`).
- The extension's server URL defaults to `http://localhost:8081/` and is
  overridable in the popup.
- Per-platform search behavior is data-driven via `agency.sql`
  (`Settings.methods[]`); no code change needed to add a locale.
