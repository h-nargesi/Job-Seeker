# Chat 14 — Reports Phase 5: Skills Gap (S4)

**Status:** done 2026-09-27. **Depends on:** Chat 13 (phase 4). Implements
[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6 roadmap step 5.

## Goal

Add the **S4 skills gap** chart to `/report/stats`: top-30 demanded skills
across all jobs carrying `AiSkills` (whole market, any State), each bar
colored green (have) or red (missing). "Have" = master-resume key name/phrase
OR a regex match against effective JobOption patterns of the included
categories. Manual reload only, per §5.

## Design pointers

[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §4 S4 spec (population,
have-reference, normalization pipeline, alias map), §5 (SQL rules, budgets).

## Files

- `core-decision-dotnet/Analyze/Models/Stats/SkillNormalizer.cs` (new) —
  `Normalize` (lowercase → strip punctuation except `# + . &` → collapse
  separators `-_/` and whitespace → conservative plural fold → alias map),
  `BuildAliasMap` (keys run through the same core pipeline so alias lookup is
  fold-consistent), `KnownSkills` (master-resume `Keys` key names + set
  phrases), and the code-level `SeedAliases` (+ `SeedJson` for the settings
  default).
- `core-decision-dotnet/Analyze/Models/Stats/StatsFullResponse.cs` — envelope
  gained `SkillsGap`; new records `StatsSkillsGap(Jobs, Top)`,
  `SkillsGapItem(Skill, Jobs, Have)`.
- `core-decision-dotnet/Database/Business/JobBusiness.Sql.cs` — added
  `Q_STATS_SKILLS` (`SELECT AiSkills ... WHERE AiSkills IS NOT NULL AND NOT
  ('[]'/'') @and@`; `@and@` = agency-title + country filters, same
  convention as pipeline health).
- `core-decision-dotnet/Database/Business/JobBusiness.Stats.cs` —
  `StatsSkillsGap(agencies, countries)`: counts each normalized phrase once
  per job in C#/LINQ (no SQLite `json_each`), Have = known-resume-skill OR
  any included pattern `IsMatch(skill)`; categories `field`/`tech`/`resume`
  only (`benefit`, `salary`, `keywords`, `reject`, `production` excluded);
  top 30 ordered by count desc then name. `SkillsTop` = 30.
- `core-decision-dotnet/Database/Business/AppSettingBusiness.cs` — new
  `SkillAliasesKey` = `skillaliases` (`SkillAliases()` reads the JSON map,
  falls back to `SkillNormalizer.SeedAliases` when absent/invalid; `Save`
  validates a strict string→string JSON object via JToken so coerced ints
  like `{"a":1}` are rejected).
- `core-decision-dotnet/Analyze/Models/SettingsModels.cs` — `AppSettingField.JsonKind`.
- `core-decision-dotnet/Views/settings.cshtml` — JsonKind fields render a
  `textarea` (prefilled with the stored value or the seed default).
- `core-decision-dotnet/Controllers/Report.Stats.cs` — `StatsFull` fills the
  `SkillsGap` section (both filters apply).
- `core-decision-dotnet/Views/stats.cshtml` — S4 card (`#skills-gap-chart`,
  520 px) above the remaining-charts placeholder; footer updated.
- `core-decision-dotnet/wwwroot/scripts/stats-chart.js` — horizontal bar,
  per-bar green/red background, tooltip `N jobs — have/missing`, legend
  hidden (colors explained in the card caption); theme change rebuilds it
  like the other charts.
- `core-decision-dotnet.Tests/CheckpointDatabase.cs` — added empty
  `AppSetting`/`JobOption` DDL (the statsfull endpoint now reads them).
- `core-decision-dotnet.Tests/SkillNormalizerTests.cs` (new),
  `JobStatsTests.cs`, `SettingsCrudTests.cs`, `ReportStatsTests.cs` — see
  Tests.
- `docs/DASHBOARD_CHARTS.md` — §6 roadmap step 5 marked done.

## Decisions recorded

- **Filters**: S4 follows both Agencies and Countries filters (the query has
  a Country dimension, unlike S1/S7); "whole market" in the spec means any
  State, not filter-free.
- **Alias map is fold-consistent**: both alias keys and skill phrases run
  through the same core pipeline before lookup, so `"reactjs"` in the map
  matches a folded `ReactJS` phrase. Canonical values are only
  trim+lowercased (never re-folded), so repairs like
  `"kubernete" → "kubernetes"` survive.
- **Conservative plural fold** on the last word only (final `.`-segment for
  dotted tokens so `node.js`/`vue.js` never fold): `ies→y`, `xes/zes/sses/
  shes/ches→drop es`, 4-letter `is`→drop `s` (`apis→api`), plain trailing
  `s` drop with `ss/us/is` guards. Over-folds (`jenkins→jenkin`) are repaired
  by seed aliases — the editable map is the correction mechanism by design.
- **Have via pattern** matches the *normalized* phrase against effective
  JobOption regexes (IgnoreCase), exactly as the spec's second reference half.
- **Master resume** = `ResumeHtml.MasterContext()` (all main keys, empty
  phrase sets), so in practice the JobOption-pattern half does the heavy
  lifting; the keys half still catches key-name skills like `java`.

## Tests

`SkillNormalizerTests`: cleaning/folding table (apis, repositories,
processes, boxes, css, redis, analysis, business), alias application, seed
aliases (`C#.NET→c#`, `dotnet→.net`), `BuildAliasMap` key/value
normalization, `KnownSkills` from master context. `JobStatsTests`:
per-job distinct counting + Have flags (pattern, resume key, missing),
excluded categories (benefit/production don't grant Have, resume does),
saved alias map merging (`Vue`+`vue.js`), agency+country filters, top-30
cap. `SettingsCrudTests`: seed fallback, custom map round-trip, strict JSON
rejections (`{ nope`, `[1,2]`, `{"a":1}`). `ReportStatsTests`: SkillsGap
section present and zero-filled on an empty DB.

## Must not

S2/S3/S5/S6/S9/S10 datasets (roadmap step 6), `docs/API.md` entries, status
header flip, TODO.md checkbox (reports still in progress), no auth/middleware
changes. The 3 `LinkedInMarkupTests` failures remain pre-existing
(gitignored `/examples/` fixtures missing locally).

## Validate

```
dotnet build Job-Seeker.sln
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```

Manual smoke (`dotnet run --project core-decision-dotnet`): `/report/stats`
renders the S4 bar list colored have/missing; Agencies/Countries + Apply
filters it; `/settings` shows the Skill aliases textarea (seed prefill) and
saving a custom map changes the grouping after reload.
