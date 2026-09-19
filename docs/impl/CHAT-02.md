# Chat 2 — Regex gate and browser loop

**Status:** not started. **Depends on:** Chat 1.

## Goal

Regex scoring writes `NotApprovedRegex` / `AiPending`. Browser job pages
follow the new states. Content-change compare on re-scrape. No `Q_INDEX` v2.

## Design pointers

[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) (`JobEligibilityHelper`,
`JobPage`, Indeed, Stepstone). [`AI_INTEGRATION.md`](../AI_INTEGRATION.md)
§4.1 / §6 (floor as state boundary). Decision log: HumanEdited skip (D1),
content Normalize, resurrection is **Chat 3** (JobBusiness). F4.

## Files

- [`Analyze/JobEligibilityHelper.cs`](../../core-decision-dotnet/Analyze/JobEligibilityHelper.cs)
  — already over ~400 lines: **split by content** while editing. Replace
  `MinEligibilityScore` (both usages) with `AppSetting` `floor` (default 70).
  Below floor → `NotApprovedRegex` (purge Html/Content as today). Floor+ →
  `AiPending`. `user_changes`: explicit `State is Rejected or Applied`.
  If `Options.HumanEdited == true`, skip `Options` overwrite; still refresh
  `Score`/`Log`. Force per-job Revaluate is Chat 4.
- [`Analyze/Pages/JobPage.cs`](../../core-decision-dotnet/Analyze/Pages/JobPage.cs)
  — empty commands for `NotApprovedRegex`; follow-up save/open keyed to
  regex-approval (`AiPending`, not `Attention`). `LoadJob`: Normalize
  compare before `SetHtml`; thread `content_changed`. Update
  `include_state` for `UpdateScrapedJob`.
- [`Analyze/Stepstone/StepstonePageJob.cs`](../../core-decision-dotnet/Analyze/Stepstone/StepstonePageJob.cs)
  — mirrors `LoadJob` / approval click.
- [`Analyze/Indeed/IndeedPageJob.cs`](../../core-decision-dotnet/Analyze/Indeed/IndeedPageJob.cs)
  — direct `NotApproved` write → `NotApprovedRegex`.
- Grep other `JobState.NotApproved` / `"NotApproved"` in Analyze/ and
  update behavior (not ranking SQL).

## Must not

Edit `Q_INDEX` / `Q_CLEAN_*` / `JobRanking` (Chat 3). No `/ai/*`. Do not
introduce `State > Attention` (or any enum-order) checks.

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`

Expect to update [`JobEligibilityHelperTests.cs`](../../core-decision-dotnet.Tests/JobEligibilityHelperTests.cs),
[`PhaseAGoldenTests.cs`](../../core-decision-dotnet.Tests/PhaseAGoldenTests.cs),
[`PhaseBNewApiTests.cs`](../../core-decision-dotnet.Tests/PhaseBNewApiTests.cs),
[`GetFirstJobTests.cs`](../../core-decision-dotnet.Tests/GetFirstJobTests.cs),
[`SalaryScoreTests.cs`](../../core-decision-dotnet.Tests/SalaryScoreTests.cs).
Add content-change Normalize tests.

## Done

Floor+ jobs become `AiPending`; below-floor `NotApprovedRegex` with content
purged; `Rejected`/`Applied` never overwritten by eval; re-scrape change
detected without a hash column.

## Starter prompt

```
Implement Chat 2 of the AI playbook.

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md (rules + slice index only),
docs/impl/CHAT-02.md, docs/AI_PHASE1_NOTES.md (JobEligibilityHelper,
JobPage, Indeed, Stepstone), docs/AI_INTEGRATION.md §4.1 and §6 floor.

Do only Chat 2. Stop at Done. Do not start Chat 3. Do not edit Q_INDEX.

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
