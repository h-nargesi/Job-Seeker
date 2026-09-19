# AI resume tailoring

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 3). Records the design for
> LLM-driven, per-job resume customization, including the decided delivery
> step (§6): the tailored resume is printed manually from the browser.
> Two-layer tailoring (2026-09-19): selection stays live; accept-gated
> text lives in `Job.ResumeText`. No AI code exists yet. Amended
> 2026-09-17 then 2026-09-19 — see the decision log
> ([`AI_DECISION_LOG.md`](AI_DECISION_LOG.md)).

## 1. How the resume works today (the part that matters)

`Views/resume.cshtml` is not one document. It is a **superset** of every
possible resume variant, and the delivered resume is produced by pruning it:

- Every block is tagged with `key-*` classes (`key-dotnet`, `key-java`,
  `key-extra-2`, `key-not-extra-3`, ...), including English (`ltr`) and
  Persian (`rtl`) variants of each experience.
- Scoring already populates a `ResumeContext` (stored as `Job.Options`):
  `Keys`, `Included`/`NotIncluded`, `Length`, `JobTitle`, `Elements`,
  `INPUT_DATA`, `PageBreak`. (`MORE`, listed here earlier, is not a context
  field — see the lever-table correction in §2.)
- Client-side JS in the template hides/shows blocks from those flags. The
  `Included`/`NotIncluded` entries are **arbitrary CSS selectors** — see
  `KeyWordContext.ClearanceMonitoring` (`#clearance-monitoring`) — so a
  single bullet is addressable today, e.g.
  `#system-group-total li:nth-child(3)`, with zero template changes.

So **selection** today = pruning a fixed superset, never text generation.
That remains layer 1. Layer 2 (2026-09-19) is a separate per-slot text
overlay (`Job.ResumeText`) for the resume title, the summary paragraph,
and job-description bullets — never HTML, never new employers or dates.

Decided (2026-09-10, selection path unchanged 2026-09-19): the AI
selection delta reuses `ResumeContext` in `Job.AiOptions` (no new
inventory table). The view resolves
`Options.HumanEdited ? Options : (AiOptions ?? Options)` (2026-09-11, §6),
then applies `ResumeText.live` over title / summary / bullets. The block
inventory for the prompt is derived from the template at runtime.

`Job.Options` is standard JSON via `ResumeContextTypeHandler`; it is the
regex-built (or human-edited) selection context and is **not** left empty
until a human edits — emptying it would drop the regex baseline after
`Html`/`Content` purge. The `HumanEdited` flag stays the signal that the
human owns selection.

## 2. Layer 1: the LLM produces a ResumeContext *delta*, not HTML

Do not put the model on the raw template (a large single file with inline
JS/CSS — a quantized model will mangle it, and diffs become unreviewable).
Instead the model receives the job description plus an inventory of the
available blocks, and emits a small, reviewable JSON delta:

```
job description (plain text)
+ block inventory (auto-derived from the template)
+ current ResumeContext
+ candidate profile (fixed system prompt)
                ▼  Qwen3, JSON-schema-constrained
{
  "keys":         ["JAVA", "SQL"],
  "notIncluded":  ["#system-group-total", ".key-front-end",
                    "#web-sites li:nth-child(2)"],
  "length":       2,
  "texts": {
    "title":   "Senior Java Backend Engineer",
    "summary": "…rewritten summary…",
    "#douran li:nth-child(2)": "…rewritten bullet…"
  }
}
```

*Illustrative (2026-09-19):* the selection half is `keys`,
`included`/`notIncluded` selectors, `length`. The optional `texts` map is
**not** stored on `ResumeContext`; the core writes proposals into
`Job.ResumeText`. The dropped `summaryOn`/`titleVariant` context fields
stay dropped. There is **no** `Job.AiTitle` column — title is the
`title` slot in `ResumeText`. The **core** applies a valid selection
delta onto `Job.Options` and stores the complete context in
`Job.AiOptions`; a valid `texts` map writes `proposal` fields; the two
halves validate independently (an invalid texts map must not drop a valid
selection delta). The worker posts the raw combined payload inside
`POST /ai/verdict`.

Why selection still beats HTML editing:

1. **Layout cannot break** — the model never touches CSS/JS.
2. **Facts cannot be invented by selection** — the model can only choose
   among real, pre-written blocks for which articles and chips appear.
3. **Reviewable** — the selection delta is a few lines; text proposals
   are per-slot diffs on job-detail.
4. **Reversible** — `AiOptions` sits beside `Options`; `ResumeText`
   proposals sit beside `live` (or the template default).

### The levers `ResumeContext` already exposes

| Lever | Controls |
|-------|----------|
| `Keys` (`DOTNET`, `JAVA`, `SQL`, ...) | Which stacks — and their matching experience articles and skill chips — appear |
| `Included` / `NotIncluded` | Any CSS selector: whole articles, skill chips, a single `<li>` |
| `Length` (1 / 2) | `key-extra-N` / `key-not-extra-N` blocks (page budget) |
| `INPUT_DATA` | Numeric overrides applied to elements carrying the matching class (e.g. years of experience) |
| `Elements` | Header items (phone, location, image, footer) |
| `PageBreak` | Page-break placement |

*Correction (2026-09-17): `MORE` is not a `ResumeContext` lever — it is a
template JS constant (`Views/resume.cshtml`) fed from job-option keywords.
Delta-contract whitelist: the model may set `Keys`, `Included`/
`NotIncluded`, `Length`; `INPUT_DATA`, `Elements` and `PageBreak` are
human-only. Text slots are not ResumeContext levers — they live in
`ResumeText` (§3).*

## 3. Layer 2: accept-gated text in `Job.ResumeText`

Decided (2026-09-19), superseding selection-only end-to-end
(2026-09-10) and the `AiTitle` column (2026-09-11 / 2026-09-17): the
model may **propose** free text for a closed set of slots, and the human
may write the same slots. Proposals **never render unaccepted**.

Slots (closed set):

1. Resume title (`title`) — one line, ≤ 80 chars (the old `AiTitle` cap).
2. Summary / intro (`summary`) — the English summary paragraph. The
   segmented-summary plan (pre-written `key-*` sentences) is dropped.
3. Job-description bullets — existing `<li>` selectors only.

`Job.ResumeText` is one JSON column (map of slot id → record). Slot ids
are `title`, `summary`, or a CSS selector that addresses one `<li>`. Each
record:

| Field | Meaning |
|-------|---------|
| `live` | Text that renders (human-written or accepted AI). Null → template default for that slot. |
| `proposal` | AI suggestion. Null when none. **Never rendered.** |
| `status` | `pending` / `accepted` / `rejected` |

Rules (locked):

- AI writes only `proposal` (and `pending`). It does not overwrite `live`
  when the slot is `accepted` or human-owned.
- Accept copies `proposal` → `live`, sets `accepted`. This is **not** a
  selection accept and does **not** set `ResumeContext.HumanEdited`.
- Reject sets `rejected`; render uses the template default; a re-run must
  not immediately restore the same proposal.
- Human edit writes `live` and `accepted` (source is the human).
- Regex eval never touches `ResumeText`. Force Revaluate (D1.4) clears
  `AiOptions` and all `proposal` fields; `live` text survives.
- Render: resolve selection (`Options.HumanEdited ? Options :
  (AiOptions ?? Options)`), then overlay every `live` value. Pending and
  rejected never appear — including on `GET /assistant/jobs` resume text.
  Phase 5 (2026-09-19): that endpoint also flags pending proposals so the
  assistant can **warn**; Fill stays allowed. Checking the resume before
  the company sees it remains the user's duty.

The 2026-09-17 "accept = full copy into `Options`, never a field-wise
merge" rule still applies to **selection** only.

Call-2 inventory (amends D16): items that are editable slots carry their
**full current template text**. Other inventory items keep the ≤ 120 char
excerpt. No item-count cap. The 16k per-call cap is unchanged; overflow
still tail-truncates the JD. Do not raise the cap speculatively.

## 4. Guardrails (non-negotiable)

1. **Selection is live; text is gated.** The model chooses among
   pre-written blocks (`keys` / `included` / `notIncluded` / `length`).
   Free text is allowed only as `ResumeText` proposals for the closed
   slot set in §3. The model never adds experience entries, employers, or
   dates, and never edits HTML/CSS/JS.
2. **Human in the loop — user duty on job-detail (2026-09-17), text
   accept-gated (2026-09-19).** There is no mechanical approval step for
   selection deltas: they are live immediately; reviewing/adjusting them
   before apply is the user's duty on job-detail (the only UI path to the
   resume). Every text proposal stays pending until explicit
   accept / reject / edit.
3. **Versioned and reversible.** `Job.AiOptions` is stored separately from
   `Options`; deleting the delta falls back to the regex-built context.
   Clearing a `ResumeText.live` value falls back to the template slot.

## 5. If direct HTML surgery is ever needed

The safe variant, should the two layers ever prove insufficient:

- The model emits whitelisted operations only —
  `hide(selector)` / `remove(selector)` / `setText(selector, text)` — and
  nothing else.
- The **server** applies them with HtmlAgilityPack (already a dependency;
  see `JobEligibilityHelper.GetTextContent`), never touching `<script>` or
  `<style>`, and validates the result.

This is strictly weaker than the delta + `ResumeText` pattern — it
bypasses `Job.Options` and the template's own show/hide logic — so treat
it as a last resort.

## 6. Flow

```
Job (State = Attention)
   │
   ▼
ai-worker call 2 (AI_INTEGRATION.md §6; runs only when the verdict
     promotes to Attention — threshold = AiPassmark)
     input: rubric + block inventory (full text on editable slots)
           + current context + JD
   │
   ▼
combined JSON  (selection: keys / included & notIncluded / length;
     texts: title / summary / li-selector → proposed wording)
   │
   ▼
POST /ai/verdict — the CORE independently:
     • validates the selection delta, applies it onto the regex-built
       Options, stores the complete context in Job.AiOptions
     • validates the texts map, writes ResumeText.proposal (pending)
     raw payload appended to job.Log
   │
   ▼
job-detail: selection diff is the user's duty (live, no approval);
     text proposals are accept / reject / edit (never live until then)
   │
   ▼
resume view renders selection, then overlays ResumeText.live
   │
   ▼
/job/resume?jobid=...
   │
   ▼
user prints from the browser — @media print CSS is in the template
```

Decided (2026-09-11) — human/AI coexistence on **selection**, unchanged
2026-09-19:

- `ResumeContext` gains a `HumanEdited` flag inside the stored JSON (the
  `Version` constant bumps with it). The flag round-trips through
  `SimlpeSerialize`/`SimlpeDeserialize` with the rest of the context —
  that "simple JSON" form is the dashboard/client **exchange** format;
  `Job.Options` itself is stored as standard JSON via
  `ResumeContextTypeHandler` (`SqliteTypeHandlers.cs`) (wording
  corrected 2026-09-18).
- Rendering precedence for selection: `Options.HumanEdited ? Options :
  (AiOptions ?? Options)` — a human selection edit always wins; otherwise
  the AI delta; otherwise the regex-built context. `Options` is never
  used as a "nullable until human edits" store.
- AI writes only `AiOptions`, never `Options`; human selection edits keep
  writing `Options` through the existing `JobController.Options` →
  `ChangeOptions` path. After a human selection edit, newer AI suggestions
  appear on the job-detail page as a diff with an explicit accept action;
  accepting is a **full copy** into `Options` (sets `HumanEdited`) — never
  a field-wise merge (2026-09-17). A re-run rewrites `AiOptions`, unless
  `Options.HumanEdited` — then the new suggestion stays a diff-only
  proposal. Text accept/reject is independent (§3).

`Applied` stays a user action; both report paths (dashboard job-detail
button `POST /job/apply`, assistant `POST /assistant/applied`) are
idempotent and `job.Log` records the source (decided 2026-09-18;
reaffirmed 2026-09-19 — never inferred from Fill or the site submit).

### Delivery (decided)

The CloudConvert HTML→PDF path (`wwwroot/scripts/resume-pdf.js`) is retired —
its API key was removed and the service is no longer used. The print surface
is `/job/resume` in any browser: the template's `@media print` rules make the
served page print-ready, so printing to paper/PDF is a manual browser step.
No server-side PDF conversion and no new `print` browser command are planned
(see [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md)).
