# Per-Agency Search Page Limit

> Status: planned, not implemented. Approved design (2026-09-30) — implement when
> convenient. Task breakdown below is implementation-ready.

Limit how deep each agency's search pagination goes. When the Nth search-results
page has been processed for the current searching method (country), stop clicking
"next" and let the existing state machine advance to the next method / finish.

## Decisions (confirmed with user)

1. **Counted unit:** number of search-results pages crawled (pagination depth).
2. **Reset scope:** per searching method (each country run gets a fresh budget of N pages).
3. **Configuration:** new "Search limit" column on the `/settings` Agencies table
   (next to Pacing), persisted in the agency's `Settings` JSON as `pageLimit`.
   Empty = unlimited (current behavior, backward compatible). No `agency.sql` seed change.

## Current flow (context for implementer)

- `SearchPage.IssueCommand` (`Analyze/Pages/SearchPage.cs`) extracts job URLs, then
  `CheckNextButton` returns `[Click(next), Recheck]` or `[]` when no next page.
- `Agency.AnalyzeContent` (`Analyze/Agency.cs:99-115`): when state is `Seeking` and
  commands are empty it advances `CurrentMethodIndex` (next country) or finishes the
  round. **The limit reuses this: returning `[]` when the cap is hit needs no new
  state-machine code.**
- Per-agency runtime settings pattern to mirror: `Waiting`/Pacing
  (`ApplyWaiting` → `AgencyBusiness.SaveWaiting` → `/settings/agencywaiting` →
  `save_waiting()` in `settings-operations.js`).

## Tasks

### 1. `Analyze/Agency.cs` — setting + counter

- `AgencySetting`: add
  `[JsonProperty("pageLimit", NullValueHandling.Ignore)] public int? PageLimit { get; set; }`.
- Counter state: `private int search_pages_visited;` and
  `private string? last_search_page_url;`.
- `public int? SearchPageLimit => settings.PageLimit;`
- `public bool SearchPageLimitReached => settings.PageLimit is int limit && limit > 0 && search_pages_visited >= limit;`
- `public void RegisterSearchPage(string url)`: under `agency_lock` (reentrant from
  `AnalyzeContent`); increment only when `url != last_search_page_url`, store the url.
  The URL dedupe keeps `Recheck` re-analysis of the same page from burning budget.
- `private void ResetSearchPages()` — zero both fields. Call it from:
  - the `CurrentMethodIndex` setter (covers method advance in `AnalyzeContent` and
    `ApplyRunning` restarts),
  - `ApplyStatus` when seeking flips off→on (new cycle),
  - `LoadSettings` (covers `ReloadSettings`).
- `public void ApplySearchPageLimit(int? limit, Database database)`: mirror
  `ApplyWaiting` — set `settings.PageLimit`, then `database.Agency.SaveSearchPageLimit(this, limit)`.

### 2. `Analyze/Pages/SearchPage.cs` — enforce

In `IssueCommand`, after the job-extraction transaction:

```csharp
Parent.RegisterSearchPage(url);

if (Parent.SearchPageLimitReached)
{
    Log.Information("Agency ({0}): search page limit ({1}) reached - method {2}",
        Parent.Name, Parent.SearchPageLimit, Parent.CurrentMethod.Title);
    return [];
}

return CheckNextButton(url, content) ?? [];
```

Returning `[]` makes `Agency.AnalyzeContent` advance the method (existing logic
saves state to DB) — no other wiring needed.

### 3. `Database/Business/AgencyBusiness.cs` — persistence

Add `SaveSearchPageLimit(Agency agency, int? limit)` mirroring `SaveWaiting`:
load JSON via `Q_LOAD_SETTING`, deserialize `Agency.AgencySetting`, set `PageLimit`,
`SaveSettings(...)`.

### 4. `Controllers/Settings.cs` + context — API

- New POST endpoint `AgencySearchLimit` mirroring `AgencyWaiting`: find agency,
  validate `SearchLimit is null or >= 1 and <= 500` (else BadRequest
  `search-limit-out-of-range`), `agency.ApplySearchPageLimit(...)`,
  `analyzer.ReloadSettings()`, return `Ok()`.
- New `Controllers/Context/AgencySearchLimitContext.cs` mirroring
  `AgencyWaitingContext.cs` (`Agency`, `int? SearchLimit`).

### 5. Settings page UI

- `Analyze/Models/SettingsModels.cs`: extend
  `AgencyPacingItem(string Name, int? Pacing, int DefaultPacing, int? SearchPageLimit)`.
- `Controllers/Settings.cs` `Index`: project `a.SearchPageLimit` into the item.
- `Views/settings.cshtml` Agencies table: add `<th>Search limit (pages)</th>`;
  per row add input `id="limit-@item.Name"` (value = limit or empty, placeholder
  `∞`), hint "empty = unlimited", Save button calling `save_limit('@item.Name')`,
  status span `limit-status-@item.Name`.
- `wwwroot/scripts/settings-operations.js`: add `save_limit(name)` mirroring
  `save_waiting` — parse int (empty → null), POST `/settings/agencysearchlimit`
  with `{ agency, searchLimit }`, reload on success.

### 6. Test (optional but cheap)

New xUnit file in `core-decision-dotnet.Tests`: fake `Agency` subclass (override
`Name`, `SearchLink`, `JobAcceptabilityChecker`, `RunningSearchingMethodChanged`,
`GetSubPages` with empty) asserting: `RegisterSearchPage` counts distinct URLs only;
`SearchPageLimitReached` flips at the configured N; `CurrentMethodIndex` change resets
the counter. No DB needed for these.

## Validation

- `dotnet build core-decision.sln`
- `dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`
- Manual: set LinkedIn limit = 2 on `/settings`, run a search trend, confirm the log
  line fires after 2 results pages and the trend moves to the next country; clear
  the field and confirm unlimited behavior returns after save.

## Known accepted behaviors

- Server restart mid-method resets the counter (that method may crawl up to N extra
  pages once).
- Saving any per-agency setting triggers `ReloadSettings`, which resets the current
  method's counter (fresh budget from the new settings).
- A manually refreshed results page under a distinct URL counts toward the budget.
