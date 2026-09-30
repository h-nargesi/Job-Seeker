# Apply-flow smoothing — Applied badge fix, resume print, deterministic pre-fill

Three user-approved changes to the apply workflow (decisions confirmed in chat,
2026-09-30; refinements confirmed same day):

1. **Applied badge fix** — in-place update on job-detail (no reload).
2. **Resume print** — dashboard-only. `/job/resume?jobid=N&print=1` auto-opens the
   print dialog (gated by param; plain `/job/resume` stays a review page), per-job
   default PDF filename via `document.title`, floating Print button on the resume
   page. The human still reviews the preview and presses Save (manual-delivery
   lock preserved). No `/assistant/resume` route (rejected for now).
3. **Deterministic pre-fill** — before the LLM loop, Fill prefills short fields
   from **confirmed** Apply-memory rows with exact `fieldKey` match (precedence
   unchanged: Correction > Tip, exact domain > `*`, UseCount, newest). Runs even
   when `llm-busy`. Pre-filled entries are removed from the LLM inventory;
   `UseCount` bumped only for applied rows. Long-text and manual/file fields are
   skipped (they stay in the Compose/LLM lane). Still no submit tool.
   Confirmed extras: on `llm-busy` accepted Compose drafts are still applied;
   when nothing remains for the LLM after pre-fill, the LLM call is skipped and a
   deterministic note is posted to chat.

Current versions (verified): core csproj `<Version>` **3.2.0**; assistant
manifest `version` **1.1.2**.

---

## A. Item 1 — Applied badge (core, Razor + static JS)

1. `core-decision-dotnet/Views/job-detail.cshtml` (line 36): add `id="job-state"`
   to the state span: `<span id="job-state" class="@job.State.CssClass()">@job.State</span>`.
2. `core-decision-dotnet/wwwroot/scripts/server-operations.js` — `apply(jobid)`:
   - Guard on `response.ok` before any DOM update (today the fetch result is
     ignored).
   - After a successful POST: if `document.getElementById('job-state')` exists
     (job-detail page), set `textContent = "Applied"`, `className = "text-success"`
     (the value of `JobState.Applied.CssClass()` — hardcode; static JS file).
   - Keep the existing dashboard branch (`#Job_<id>` className + `LoadJobs()` +
     `LoadAgencies()`, cf. `jobs.cshtml:6,24`) unchanged — those elements are
     absent on job-detail.
3. Optional parity (same 3 lines, not in TODO): `reject()` updates the badge to
   `Rejected` / `text-danger` the same way. Do it only if trivial.

## B. Item 2 — resume print (core, Razor view + model flag)

**Critical constraint:** `resume.cshtml` is rendered to a string in three
non-interactive paths — `MasterResumeCache` (master resume injected into every
AI ranking prompt), `AssistantController.Job` (resume text for Fill/Compose),
and `JobController.Resume64` (HTML file download). `JobContent.GetTextContent`
skips `head` (so the `<title>` change is safe) but includes **body text** — an
always-present button would pollute the master resume and every Fill prompt.

1. `core-decision-dotnet/Analyze/Models/ResumePage.cs`: add
   `public bool Interactive { get; init; }` (default `false`).
2. `JobController.Resume` (only this caller): set `Interactive = true` on the
   `ResumePage` it returns. `Resume64`, `AssistantController.Job`, and
   `MasterResumeCache` keep the default `false`.
3. `core-decision-dotnet/Views/resume.cshtml`:
   - **Per-job default filename**: replace the static `<title>` with
     `@System.IO.Path.GetFileNameWithoutExtension(Model.Context.FileName("pdf"))`
     → e.g. `hamed-nargesi-resume-3-df`. Chrome uses `document.title` as the
     suggested "Save as PDF" filename. Keep the existing beforeprint/afterprint
     handlers (dashify spaces; harmless).
   - Wrap **both** new interactive pieces in `@if (Model.Interactive)`:
     - **Floating Print button**: fixed-position (e.g. bottom-right), inline CSS +
       plain `onclick = () => window.print()`, English label
       "Print / Save as PDF", hidden under `@media print`. No bootstrap.js.
     - **Auto print dialog**: if
       `new URLSearchParams(location.search).get("print") === "1"`, then when
       `document.readyState === "complete"` call `window.print()` immediately,
       else register a one-time `window.addEventListener("load", () => window.print())`
       (waits for the header background image / web fonts).
4. `Views/job-detail.cshtml` (next to "Resume as html", line ~55): add
   `<a href="/job/resume?jobid=@job.JobID&print=1" target="_blank" class="btn btn-outline-primary">Print resume</a>`.
5. Version bump: `core-decision.csproj` `<Version>` 3.2.0 → 3.3.0.

## C. Item 3 — deterministic pre-fill (assistant-extension)

1. **New file** `assistant-extension/controllers/prefill.js` (classic script, no
   build; keep files under the ~400-line budget):
   - `Prefill.Matches(inventory, rows, domain)` — **pure**: for every inventory
     entry with a truthy `fieldKey`, `!entry.manual`, `!entry.longText`: filter
     rows with exact case-insensitive `fieldKey` equality and
     `agencyDomain === domain || agencyDomain === "*"`; sort with the existing
     `MemoryTools.Precedence`; take the best row; skip when `value` is empty.
     Returns `[{ fieldId, fieldKey, value, memoryID }]` (note: rows carry
     **`memoryID`**, cf. `fill-loop.js:220` — not `id`). One match per inventory
     entry — duplicate fieldKeys on the page (email + confirm-email) each fill.
   - `Prefill.Run(messaging, tabSend, tabId, domain, inventory, snapshot)` —
     `rows = await snapshot()`; on snapshot error return
     `{ prefilled: 0, matches: [] }` with a warn log (LLM pass proceeds as
     today). For each match: `tabSend(tabId, { title: "apply-fill",
     params: { fieldId, value } })`; on `ok` count it and fire-and-forget
     `messaging.MemoryBump(match.memoryID)` (same pattern as FillLoop's bump).
     Returns `{ prefilled, matches }` (matches = the applied ones).
2. `assistant-extension/controllers/background.js`:
   - `importScripts("./prefill.js")`.
   - `case "fill"`: no longer wholly wrapped in `WithLlm`. New `RunFill` order:
     1. Inventory extraction via `TabSend` (outside the LLM lock).
     2. `Prefill.Run(...)` — always, no LLM needed.
     3. `const remaining = state.inventory.filter(e => !prefilledFieldIds.has(e.fieldId))`.
     4. **Skip-LLM rule**: if `remaining` has no non-`manual` entries, do not
        call the LLM at all; result `{ done: true, prefilled, filled: 0, writes: 0 }`.
     5. Else LLM phase inside `WithLlm(() => FillLoop.Run({ ..., inventory: remaining, ... }))`.
        - `WithLlm` returns `error: "llm-busy"` → keep going (see drafts below),
          final result `{ prefilled, filled: 0, writes: 0, drafted, error: "llm-busy" }`.
     6. `ApplyAcceptedDrafts(...)` runs **after** the LLM phase in every path
        (never before — the LLM would overwrite an accepted draft on the same
        fieldKey); on the busy path it runs after pre-fill with no LLM.
     7. Merge counts (`filled` = LLM fills; `prefilled`, `drafted` separate);
        start/end logs include `prefilled N`.
     - The `write` callback keeps using the **unfiltered** `state.inventory` for
       label lookup by fieldKey.
3. `assistant-extension/application/panel.js` — `FillCurrentTab`:
   - Success line: `prefilled N from memory, filled M field(s), W memory write(s), D long answer(s) — review, then submit yourself`.
   - `result.error === "llm-busy"`: status
     `prefilled N from memory, D long answer(s) — llm-busy, LLM fields skipped`
     + short `ChatUi.Report` note (not an error dump).
   - Skip-LLM path (`done` without `content`): post a deterministic chat note
     ("all short fields prefilled from memory — review, then submit yourself");
     `if (result?.content)` guard already tolerates missing content.

### Guardrails (must hold)
- Only **confirmed** rows: snapshot is `MemoryList("Apply", true)` — unchanged.
- Exact `fieldKey` match only; no label fuzzy matching.
- Reuse `MemoryTools.Precedence` — do not duplicate the sort.
- `UseCount` bumps only for rows actually applied (2026-09-11 law).
- Human still presses every submit; prefill adds no new tool, no new manifest
  permission, no LLM change.

## D. Docs (implementation agent edits; decision log is append-only)

1. Append one `docs/AI_DECISION_LOG.md` entry (2026-09-30) covering:
   - Resume page gains `?print=1` (auto print-dialog deferred to window `load`),
     per-job default PDF filename via `document.title`, floating Print button
     gated by a new `ResumePage.Interactive` flag (string-rendered paths —
     master resume, assistant resume text, `Resume64` — stay clean); new
     `Print resume` button on job-detail. Annotates the "manual print" /
     "No print PageAction" locks: the dialog auto-opens but the human still
     reviews and presses Save; no PageAction added; dashboard-only (the
     `/assistant/resume` route idea rejected for now).
   - Deterministic pre-fill (the AI_APPLY_ASSISTANT §7 deferred item) is now
     implemented: confirmed exact-key short fields, precedence unchanged, runs
     on `llm-busy` (accepted Compose drafts still applied, LLM fields skipped),
     LLM call skipped when nothing remains, prefilled entries excluded from the
     LLM inventory, UseCount bump; long-text/manual stay in the Compose/LLM
     lane; still no submit tool.
2. `TODO.md`: mark the Applied-badge item `[×]` (keep the RTL wrapper intact).
3. Optional one-liner in `AGENTS.md` §6 resume row: mention `?print=1`.

## Tests

- New `assistant-extension/tests/prefill.test.js` (node:test + vm, mirror
  `fill-loop.test.js` patterns):
  - `Matches`: precedence (Correction beats Tip; exact domain beats `*`; higher
    UseCount wins), skips `longText`/`manual`/empty `fieldKey`/empty `value`,
    exact-key-only (no substring), duplicate fieldKey entries both matched,
    returns `memoryID`.
  - `Run` with stub `tabSend`/`messaging`: apply-fill sent per match, bump
    called with `memoryID`, snapshot-error path returns zero and does not throw.
- Extend `tests/background.test.js`: llm-busy path returns `{ prefilled, drafted }`
  and still applies drafts; LLM inventory excludes prefilled fieldIds;
  skip-LLM path when only manual fields remain; merged counts.
- Update `tests/panel.test.js` if it asserts the Fill status line.

## Validation

1. `dotnet build core-decision.sln` (Razor views compile).
2. `dotnet test core-decision-dotnet.Tests/core-decision-dotnet.Tests.csproj`
   (expected unaffected).
3. `cd assistant-extension && npm test`.
4. Manual smoke:
   - job-detail Applied updates the badge in place (dashboard list row still
     updates; no reload).
   - "Print resume" opens the dialog with the per-job default filename and the
     floating button hidden in the preview; plain "Resume as html" does not open
     the dialog.
   - **Prompt-pollution regression**: `/assistant/job?jobid=N` `resumeText` (and
     a worker master-resume run) contain no "Print / Save as PDF" button text.
   - Fill on a form with a confirmed memory row prefills it before the LLM and
     reports `prefilled`; with `LLM_BUSY` set, Fill prefills deterministic
     fields, applies accepted drafts, and reports `llm-busy, LLM fields skipped`;
     a fully-prefilled form skips the LLM call entirely.

## Out of scope (recorded, not built)

- `/assistant/resume` route under the Assistant key (panel Resume button).
- Long-text deterministic fill; memory-coverage % UI.
- Per-site ATS adapters; file-upload automation; submit automation (locked out).

## Risks / notes

- Deterministic pass fills over pre-existing control values — identical to the
  LLM path today (inventory carries no current values); accepted parity.
- `window.print()` from script opens Chrome's print preview without a user
  gesture; the Save destination ("Save as PDF") is remembered by Chrome, first
  use per profile needs one manual selection.
- `ResumeContext.FileName` includes the live `Version` and selected keywords, so
  the suggested PDF name differs per job selection — intended.
- `reject()` badge parity is optional; `change_state`/`requeue`/`promote` already
  reload and are out of scope.
