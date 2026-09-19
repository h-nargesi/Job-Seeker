# Chat 6 — Phase 3 resume tailoring

**Status:** not started. **Depends on:** Chat 5.

## Goal

Call 2 when the verdict will promote to `Attention`. Core applies selection
to `AiOptions` (live) and text to `ResumeText.proposal` (gated). Resume view
overlays `live` only.

## Design pointers

[`AI_RESUME_TAILORING.md`](../AI_RESUME_TAILORING.md) (all).
[`AI_INTEGRATION.md`](../AI_INTEGRATION.md) §4.3 / §6 two-call contract.
[`AI_PHASE1_NOTES.md`](../AI_PHASE1_NOTES.md) RubricTailor v1, D16 inventory,
F2 activate `delta`. Decision log 2026-09-17 (HumanEdited / full-copy
accept), 09-19 (two-layer, no `AiTitle`).

## Files

- `/ai/next`: add block inventory + current `ResumeContext` + settings
  needed for call 2. Inventory: full template text on editable slots
  (title, summary, job-description `<li>`); ≤120 char excerpt on other
  items; no item-count cap. Reuse Chat 4 prune/strip helper (F5).
- `POST /ai/verdict`: validate `delta` independently —
  `keys` ⊆ `MainKeys`; selectors ⊆ inventory; `length` ∈ {1,2}; caps ≤8
  keys / ≤30 selectors. Apply onto regex `Options`, store **complete**
  context in `AiOptions`. Valid `texts` → `ResumeText.proposal` +
  `pending` (title one line ≤80; no HTML). Invalid texts must not drop a
  valid selection. Invalid selection after worker retries: drop delta,
  keep verdict. Non-promoting or `Error` verdict: drop any delta. Append
  raw payload to `job.Log`.
- `ai-worker`: call 2 only if call 1 would promote (relevance ≥
  `aipassmark`). `Llm:RubricTailor`. Same Temperature/Seed. Call-2
  connection failure aborts run (write nothing). Model-output failure:
  retry twice then post verdict alone.
- [`Views/job-detail.cshtml`](../../core-decision-dotnet/Views/job-detail.cshtml)
  — selection diff is user duty (live, no mechanical approve). Text slots:
  accept / reject / edit. Accept copies proposal → live. Human selection
  edit still `ChangeOptions` + `HumanEdited`; later AI selection is
  diff-only until **full copy** accept into `Options`.
- [`Views/resume.cshtml`](../../core-decision-dotnet/Views/resume.cshtml)
  — precedence `Options.HumanEdited ? Options : (AiOptions ?? Options)`,
  then overlay every `ResumeText.live`. Pending/rejected never render.
- Force Revaluate (Chat 4): clear `AiOptions` and all `proposal`; `live`
  survives.
- Tests: independent half-validation; reject/accept; HumanEdited skip;
  non-promoting drops delta.

## Must not

Invent HTML/CSS. `AiTitle` column. Mechanical approval gate on selection.
Phase 5 memory injection (empty reserved slots remain until Chat 7).

## Validate

`dotnet build "Job Seeker.sln"`
`dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`

Resume print CSS already exists — do not add server PDF.

## Done

Promoted jobs can have live `AiOptions` and pending text; resume HTML never
shows proposals; rejected jobs never paid for call 2.

## Starter prompt

```
Implement Chat 6 of the AI playbook (phase 3 tailoring).

Read: AGENTS.md, docs/AI_IMPLEMENTATION.md, docs/impl/CHAT-06.md,
docs/AI_RESUME_TAILORING.md, docs/AI_INTEGRATION.md §4.3 and §6 two-call
contract, docs/AI_PHASE1_NOTES.md RubricTailor v1 and D16.

Do only Chat 6. Stop at Done. Do not build assistant-extension or memory.

Validate: dotnet build "Job Seeker.sln"
dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj
```
