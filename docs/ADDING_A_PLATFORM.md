# Adding a new job platform

A "platform" is a job-board site (LinkedIn, Indeed, Bayt, Stepstone, IamExpat,
Glassdoor). Adding one is mostly **plumbing**: mirror an existing platform's
folder, fill in site-specific regexes, and seed an `Agency` row. No central
registry needs editing — the `Agency` and `Page` subclasses are found by
reflection.

Worked reference: `Analyze/LinkedIn/` (full set) and `Analyze/IamExpat/`
(minimal, no login flow).

## 1. Plan the page roles the site needs

Map the site's pages onto the abstract roles in `Analyze/Pages/`. You rarely
need all of them:

| Role (abstract) | Implement when... |
|-----------------|-------------------|
| `LoginPage` | The site requires a login form you must fill |
| `AuthPage` | There's an auth/consent interstitial that should redirect to login |
| `SearchPage` | **Always** — this discovers job links from result pages |
| `JobPage` | **Always** — this scrapes a single job and triggers scoring |
| `OtherPages` | Optional catch-all for states you want to ignore |

`SearchPage` and `JobPage` are the two essentials. `LoginPage`/`AuthPage` can be
omitted if the site needs no login (see IamExpat).

## 2. Create the folder `Analyze/<Platform>/`

One file per page role, plus an agency class and a shared regex interface.
Naming convention: `<Platform>.cs`, `<Platform>Page.cs`, `<Platform>Page<Role>.cs`.
Namespace: `Photon.JobSeeker.<Platform>`.

### `<Platform>.cs` — the agency class

```csharp
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.MySite;

class MySite : Agency
{
    public override string Name => "MySite";                       // must match Agency.Title in DB

    public override string SearchLink => $"{BaseUrl}/jobs";        // where the browser opens search

    public override Regex? JobAcceptabilityChecker =>
        MySitePage.reg_job_expired;                                // null if the site has no expiry marker

    // Called when the active searching method (locale) changes.
    // Rebuild any locale-dependent regexes here from CurrentMethod.Url.
    protected override void RunningSearchingMethodChanged(int value)
    {
        var loc = Uri.EscapeDataString(CurrentMethod.Url);
        MySitePage.reg_search_location_url = new Regex(@$"(^|&)loc={loc}(&|$)", RegexOptions.IgnoreCase);
    }

    // Return THIS platform's page classes (reflection discovers them).
    protected override IEnumerable<Type> GetSubPages()
        => TypeHelper.GetSubTypes(typeof(MySitePage));
}
```

Key contracts from the `Agency` base (`Analyze/Agency.cs`):
- `Name` MUST equal the `Title` column in the `Agency` table, else the agency
  loads with `ID == 0` and is skipped.
- `BaseUrl` defaults to `Link`; override if the site needs a locale prefix
  (IamExpat does this).
- `CurrentMethod` / `CurrentMethodIndex` / `SearchingMethodCount` come from the
  `Settings.methods[]` JSON in `agency.sql` — see step 4.

### `<Platform>Page.cs` — shared regexes

Use a `internal interface` (the existing convention) to hold the compiled
regexes that both the search and job pages share. Mark locale-dependent ones
`internal static` so `RunningSearchingMethodChanged` can reassign them.

```csharp
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.MySite;

interface MySitePage
{
    protected static readonly Regex reg_search_url =
        new(@"^https?://[^/]*mysite\.com/jobs", RegexOptions.IgnoreCase);

    internal static Regex reg_search_location_url =
        new(@"(^|&)loc=default(&|$)", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_url =
        new(@"/jobs/(\d+)", RegexOptions.IgnoreCase);   // group 1 = the job Code

    protected static readonly Regex reg_job_title =
        new(@"<h1[^>]*>([^<]*)</h1>", RegexOptions.IgnoreCase);

    public static readonly Regex reg_job_expired =
        new(@"\bclosed\b", RegexOptions.IgnoreCase);     // exposed via Agency.JobAcceptabilityChecker
}
```

### `<Platform>PageSearch.cs`

```csharp
using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.MySite;

class MySitePageSearch(MySite parent) : SearchPage(parent), MySitePage
{
    protected override bool CheckInvalidUrl(string url, string content)
        => !MySitePage.reg_search_url.IsMatch(url);

    // Return true (with commands) when the URL is a search page but the
    // filters/locale are wrong — emit a Command.Go to the corrected URL.
    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (!MySitePage.reg_search_location_url.IsMatch(url))
        {
            commands = [Command.Go(@$"/jobs?loc={parent.RunningUrl}")];
            return true;
        }
        commands = null;
        return false;
    }

    // Extract (url, code) pairs from the listing HTML. code is the stable job id.
    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        foreach (Match m in MySitePage.reg_job_url.Matches(content).Cast<Match>())
            yield return (string.Join("", Parent.BaseUrl, m.Value), m.Groups[1].Value);
    }

    // Find and click the "next page" control; return [] if there is none.
    protected override Command[] CheckNextButton(string url, string content)
    {
        var m = Regex.Match(content, @"<a[^>]+class=""next""[^>]*>");
        return m.Success ? [Command.Click("a.next"), Command.Recheck()] : [];
    }
}
```

`SearchPage` (base) already handles: dedup, and saving each found job with
`State = Saved`. You only provide the extraction.

### `<Platform>PageJob.cs`

```csharp
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.MySite;

class MySitePageJob(MySite parent) : JobPage(parent), MySitePage
{
    protected override bool CheckInvalidUrl(string url, string content)
        => !MySitePage.reg_job_url.IsMatch(url);

    // Parse the job id out of the URL.
    protected override string GetJobCode(string url)
    {
        var m = MySitePage.reg_job_url.Match(url);
        return m.Success ? m.Groups[1].Value : "";
    }

    // Extract code (for shortlinks), apply link, and title from the HTML.
    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;
        apply = null;
        var m = MySitePage.reg_job_title.Match(html);
        title = m.Success ? m.Groups[1].Value : "";
    }

    // Reduce the raw page HTML to just the job-relevant subtree.
    public override string GetHtmlContent(string html) => html;   // trim wrappers as needed

    // Optional: extra clicks after a job scores as "Attention" (e.g. Save).
    protected override Command[]? JobFallow(string content) => null;
}
```

`JobPage` (base) handles loading/creating the job row, calling the scoring
engine, and persisting. You provide parsing only.

### Optional: `<Platform>PageLogin.cs` / `PageAuth.cs` / `PageOther.cs`

Only if the site needs login. Override `LoginCommands()` (fill username/password
selectors via `GetUserPass()`, then `Command.Click` the submit button). See
`LinkedInPageLogin.cs` for a real example.

## 3. Verify it compiles

```bash
dotnet build core-decision.sln
```

Reflection will pick up the new classes automatically — there is no switch to
flip. `Analyzer.LoadAgencies()` instantiates every `Agency` subclass; each
agency's `LoadPages()` discovers its `<Platform>Page` subclasses.

## 4. Seed the agency row in `database/structure/agency.sql`

Add an `INSERT` row. The `Title` must match `Name` exactly. `Domain` is a regex
the extension uses to match hostnames. `Settings.methods[]` lists the locales to
search (each becomes a "searching method"):

```sql
insert into Agency (Title, Domain, Link, UserName, Password, Settings)
values
    ('MySite', '(.+\.)?mysite\.com$', 'https://mysite.com', 'username', 'password',
        '{ "running": 0, "methods": [
            { "Title": "NL", "Url": "netherlands" },
            { "Title": "DE", "Url": "germany" }]
        }');
```

- `methods[].Title` becomes the job's `Country` value and the locale label.
- `methods[].Url` is the locale-specific fragment your `SearchLink` /
  `CheckInvalidSearchTitle` consume via `CurrentMethod.Url`.
- Credentials normally live in the gitignored `passwords.sql`.

Then reseed: `cd database && bash installation.sh`.

## 5. Test end-to-end

1. Start the server: `dotnet run --project core-decision-dotnet`.
2. Load/refresh the extension and open its popup; confirm the server URL.
3. `GET http://localhost:8081/decision/scopes` should now list your agency.
4. Navigate the browser to the site's search page. Watch the DevTools console
   for `AGENT ...` logs — the extension will POST to `/decision/take` and log the
   returned commands. There is no automated test; this manual loop is the only
   validation.

## Checklist

- [ ] `<Platform>.cs` — `Name` matches DB `Title`
- [ ] `<Platform>Page.cs` — shared regexes (incl. `reg_job_url` with the code group)
- [ ] `<Platform>PageSearch.cs` — URL validation + job extraction + next page
- [ ] `<Platform>PageJob.cs` — code/apply/title extraction + HTML trimming
- [ ] (optional) login/auth/other pages
- [ ] `agency.sql` row seeded, `installation.sh` re-run
- [ ] `dotnet build core-decision.sln` passes
