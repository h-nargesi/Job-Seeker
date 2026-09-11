# AGENTS.md

Guidance for AI coding agents working in this repository. Read this first.

> This is a personal job-search automation system. A Chrome extension drives a
> browser across several job-board platforms, sends each page's HTML to a local
> .NET server, the server **scrapes, scores, and ranks** the jobs, and decides
> what the browser should do next (navigate, paginate, save, apply). It also
> generates tailored resumes from matched job keywords.

## 1. Repository layout

```
Job-Seeker/
├── core-decision-dotnet/      # ASP.NET Core 8 server (namespace: Photon.JobSeeker)
│   ├── Program.cs             # app entrypoint, DI registration
│   ├── Controllers/           # HTTP API + Razor views (Decision, Job, Report)
│   ├── Analyze/               # scraping + scoring engine (the core logic)
│   │   ├── Analyzer.cs        # singleton: loads agencies, runs analysis
│   │   ├── Agency.cs          # abstract base for each job platform
│   │   ├── <Platform>/        # one folder per platform (LinkedIn, Indeed, ...)
│   │   ├── Pages/             # abstract page roles (Login/Search/Job/Auth/Other)
│   │   ├── JobEligibilityHelper.cs   # scoring engine
│   │   ├── TrendsCheckpoint.cs       # workflow state machine
│   │   ├── Models/            # Job, JobOption, Trend, ResumeContext, enums
│   │   └── Result/            # Command / Result / PageAction (browser protocol)
│   ├── Database/              # SQLite access (Dapper, NO EF Core)
│   │   ├── Database.cs        # connection + generic Insert/Update/Read
│   │   ├── Dictionaries.cs    # English-word DB for language detection
│   │   └── Business/          # per-entity repos (Job/Agency/Trend/JobOption)
│   ├── Basics/                # helpers (Extensions, TypeHelper, BadJobRequest)
│   ├── Views/                 # Razor views (index, jobs, trends, resume, ...)
│   └── wwwroot/               # static assets
├── core-decision-dotnet.Tests/  # xUnit tests (e.g. JobRanking breakpoint tests)
├── agent-extension/           # Chrome MV3 extension (vanilla JS, no build)
│   ├── manifest.json
│   ├── controllers/           # check-page, background, messaging, action-handler
│   └── application/           # popup UI (menu.html / menu.js)
├── assistant-extension/       # (planned, not created yet) apply-assistant MV3
│                              # extension — see docs/AI_APPLY_ASSISTANT.md
├── database/                  # SQLite schema + seed data
│   ├── structure/             # agency.sql, job.sql, job-option.sql, trend.sql
│   ├── installation.sh        # creates/reseeds the schema
│   └── data.sqlite3           # main DB (gitignored)
├── core-decision.sln          # VS solution
└── job-seeker.sh              # runs the published binary on port 8081
```

## 2. Build & run

The server is .NET 8. There is **no JS build step**. Tests live in
`core-decision-dotnet.Tests` (xUnit).

```bash
# Build (from repo root)
dotnet build core-decision.sln

# Run the server (development)
dotnet run --project core-decision-dotnet

# Production (what job-seeker.sh does)
dotnet publish core-decision-dotnet -c Release
./publish/job-seeker --urls "http://*:8081"
```

Assembly name is `job-seeker` (set in the `.csproj`), not the folder name.

**Database first run** — the schema and seed data must be loaded once:
```bash
cd database && bash installation.sh
```
This runs the `.sql` files in `database/structure/` against `data.sqlite3`.
`passwords.sql` is gitignored (contains real agency credentials) — create it if
absent, otherwise `installation.sh` will error on the last line.

**Auth secrets** — two secrets configure single-user auth + credential
encryption (`appsettings.json` ships them empty; never commit real values):

- `Auth:ApiKey` — shared secret. Extension sends it as `X-API-Key`; the
  dashboard login (`/auth/login`) uses it as the password. In **production the
  server refuses to start without it**; in Development it may be omitted (auth
  disabled, warning logged).
- `Auth:CredentialKey` — 32-byte base64 key (AES-GCM) encrypting agency
  passwords at rest (`enc:` prefix, auto-migrated on startup). Generate with
  `openssl rand -base64 32`. Same fail-fast rule in production.

```bash
# Development (user-secrets, run inside core-decision-dotnet/)
dotnet user-secrets set "Auth:ApiKey" "some-random-secret"
dotnet user-secrets set "Auth:CredentialKey" "$(openssl rand -base64 32)"

# Production (environment variables)
export Auth__ApiKey="some-random-secret"
export Auth__CredentialKey="<openssl rand -base64 32>"
```

SQLite runs in WAL mode: `data.sqlite3-wal` / `data.sqlite3-shm` files appear
next to the DB and are normal — include them in backups/restores.

**Load the extension** — `chrome://extensions` → Developer mode → Load unpacked
→ select `agent-extension/`. Set the server URL (default
`http://localhost:8081/`) and, when `Auth:ApiKey` is configured, the matching
API Key in the popup menu (press Enter in each field to save).

## 3. Lint / typecheck / test

| Check        | Command | Notes |
|--------------|---------|-------|
| Compile      | `dotnet build core-decision.sln` | Primary validation gate. Warnings are treated seriously (`<Nullable>enable</Nullable>`). |
| Extension JS | none    | Plain JS loaded directly by Chrome. Verify by loading the unpacked extension and watching the `AGENT` console logs. |
| Tests        | `dotnet test core-decision.sln` | xUnit project `core-decision-dotnet.Tests` (first in repo). |

After editing C#, **always run `dotnet build core-decision.sln`** before declaring done.

## 4. How the pieces talk (the request loop)

This is the single most important concept. The extension and server form a
closed loop:

```
extension (check-page.js)                server (DecisionController.Take)
  on page load ─────────────────────────────►  POST /decision/take
  body: { agency, url, content:<fullHTML> }    │
                                               ▼  Analyzer.Analyze(context)
                                          1. find Agency by name
                                          2. Agency.AnalyzeContent(url, content)
                                             → first matching Page.IssueCommand()
                                          3. TrendsCheckpoint decides next action
                                               │
  ◄───────────────────────────────────────────  200 { trend, commands[] }
  ActionHandler runs commands                      (go/open/fill/click/recheck/close/...)
  page changes → loop repeats
```

- The extension matches the current hostname against agency `Domain` regexes
  fetched from `GET /decision/scopes`.
- `commands[]` is the **only** way the server drives the browser. See
  `Command.cs` and `PageAction` for the full vocabulary, and `action-handler.js`
  for how each is executed.
- A `trend` id binds a logical workflow (login → search → job) to a browser tab.

Full detail: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## 5. Code conventions

- **Namespace**: `Photon.JobSeeker`. Platform sub-namespaces are
  `Photon.JobSeeker.<Platform>` (e.g. `Photon.JobSeeker.LinkedIn`). Page-base
  sub-namespace is `Photon.JobSeeker.Pages`.
- **No comments** unless requested (matches existing style). Do not add XML doc
  comments gratuitously.
- **File-size budget (~400 lines).** No file may grow unbounded — every line is
  token cost each time an agent reads the file. When a file exceeds the budget,
  split it by content, never mechanically by line count: move documentation
  prose into `docs/` (or an archive doc there for historical material), or
  break an oversized class into multiple cohesive classes/files while respecting
  clean-code and design-pattern rules (single responsibility first). This
  applies to `AGENTS.md` itself — prune or archive instead of appending.
- **Raw SQLite via Dapper (no EF Core).** `Database.cs` wraps a `SQLiteConnection`
  and exposes Dapper `Execute`/`Query<T>`/`ExecuteScalar` plus transactions and
  `LastInsertRowId()`/`Changes()`. Business classes in `Database/Business/` own
  explicit typed write methods (`InsertJob`, `UpdateScrapedJob`, `CreateTrend`,
  `SaveSettings`, ...) — one per call-site shape; there is no reflection-based
  Insert/Update and no column filter enums.
- **Enum columns store the enum *name* as text** (`'Attention'`, `'Seeking'`),
  never the int. Reads go through `SqliteTypeHandlers` (registered in the
  `Database` static ctor); SQL parameters must pass enums as
  `someState.ToString()` — Dapper binds raw enum parameters as integers.
  `ResumeContext` columns round-trip as JSON via the same handler file.
- **Reflection-discovered plugins.** `TypeHelper.GetSubTypes(typeof(Agency))`
  finds every platform class automatically. Adding a platform = adding a class;
  no registration list to edit. Same for `Page` subclasses per platform.
- **C# 12 / file-scoped namespaces / primary constructors** are in use.
  Match the surrounding style.
- **Nullable enabled.** Don't silence nullability with `!` unless necessary.
- **Technical docs (`docs/`, `AGENTS.md`) are English-only.** Personal working
  logs (`REVIEW*.md`) may be Persian; anything an agent reads must be English.

## 6. Common tasks (pointers)

| Task | Start here |
|------|-----------|
| Add a new job platform (site) | [`docs/ADDING_A_PLATFORM.md`](docs/ADDING_A_PLATFORM.md) + mirror any `Analyze/LinkedIn/` folder |
| Change scoring rules / keywords / weights | `database/structure/job-option.sql` (re-seed) + `JobEligibilityHelper.EvaluateEligibility` |
| Change what the browser does (new command) | `Result/Command.cs` + `PageAction` enum + `action-handler.js` |
| Add a country/locale to a platform | `agency.sql` → that platform's `Settings.methods[]` |
| Resume generation | `Views/resume.cshtml` + `ResumeContext` + `JobController.Resume` / `Resume64`; printing is manual from the browser (CloudConvert retired) |
| Add an AI-assisted (local LLM) stage | [`docs/AI_INTEGRATION.md`](docs/AI_INTEGRATION.md) + [`docs/AI_RESUME_TAILORING.md`](docs/AI_RESUME_TAILORING.md) (design + decision log, not implemented; phase 1 = worker + verdict + extraction + ranking) |
| Fill apply forms with the assistant (planned) | [`docs/AI_APPLY_ASSISTANT.md`](docs/AI_APPLY_ASSISTANT.md) (design only, not implemented; second extension, personal terminal) |
| API surface | [`docs/API.md`](docs/API.md) |
| Scoring & state machines | [`docs/SCORING.md`](docs/SCORING.md) |

## 7. Things that bite

- **`dictionaries.sqlite3` lives one level up.** `appsettings.json` points at
  `../database/dictionaries.sqlite3`; it is NOT in `database/structure/` and is
  not created by `installation.sh`. Language detection (`LanguageIsMatch`)
  silently degrades if it is missing.
- **`State` columns store the enum *name* as text** (`'Attention'`,
  `'Seeking'`), not the int. SQL must compare against the name string.
  `JobBusiness` builds queries with `nameof(JobState.X)` to stay in sync.
- **`Agency.Active` is a bitfield** (`AgencyStatus`: 1=Seeking, 2=Analyzing,
  3=both). `0` means inactive and the agency is excluded from `Agencies` (the
  name-keyed dict) but kept in `AgenciesByID`.
- **`job-seeker.sh` hardcodes a Linux publish path.** Don't trust it on Windows;
  use `dotnet run` instead.
- The dashboard SQL in `JobBusiness.Q_INDEX` ranks jobs with a time-decay curve.
  Touch with care — it references enum names and the `Relocation` log marker.
