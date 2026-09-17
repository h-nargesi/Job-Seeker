# AI resume tailoring

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 3). Records the design for
> LLM-driven, per-job resume customization, including the decided delivery
> step (§6): the tailored resume is printed manually from the browser, and
> the decided selection-only summary (§3). No AI code exists yet. Amended
> 2026-09-17: phase-3 ambiguities resolved — see the decision log
> ([`AI_DECISION_LOG.md`](AI_DECISION_LOG.md), "Phase-3 ambiguities
> resolved").

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

So customization today = **selection over a fixed superset**, never text
generation. That is exactly the hook the LLM plugs into.

Decided (2026-09-10): this mechanism does not change. The AI delta reuses
the same `ResumeContext` structure in `Job.AiOptions` (a second
ResumeContext — no new inventory table), the view resolves
`Options.HumanEdited ? Options : (AiOptions ?? Options)` (2026-09-11, §6),
and the block inventory for the prompt is derived from the template
automatically at runtime.

## 2. The pattern: the LLM produces a ResumeContext *delta*, not HTML

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
  "summaryOn":    ["java-backend", "14-years", "distributed"],
  "titleVariant": "java-backend"
}
```

*Illustrative only (corrected 2026-09-17 — decision log, "Phase-3
ambiguities resolved"): the final delta contract is `keys`,
`included`/`notIncluded` selectors, `length` — plus summary-segment and
title-variant selection expressed as template-element selections, and an
optional guarded free-text title string. The `summaryOn`/`titleVariant`
context fields shown above are dropped (no new `ResumeContext` fields). The
**core** applies the delta onto `Job.Options` and stores the complete
resulting context in `Job.AiOptions`; the worker only posts the raw delta
inside `POST /ai/verdict`.*

The delta is applied to a **separate** `Job.AiOptions` field; rendering and
template stay unchanged. The resume view resolves
`Options.HumanEdited ? Options : (AiOptions ?? Options)` (§6), so the
regex-built context always remains the fallback.

Why this beats HTML editing:

1. **Layout cannot break** — the model never touches CSS/JS.
2. **Facts cannot be invented** — the model can only select among real,
   pre-written blocks; it cannot create experience, employers, dates, or
   numbers. On a factual document like a resume, that is the line between
   tailoring and fabrication.
3. **Reviewable** — the delta is a few lines, previewable on the dashboard.
4. **Reversible** — `ResumeContext.Version` (currently 42) is the existing
   versioning hook; the delta lives beside the original, never over it.

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
`NotIncluded`, `Length` and select summary/title template elements;
`INPUT_DATA`, `Elements` and `PageBreak` are human-only.*

## 3. Selection over pre-written text — the summary becomes segmented

Decided (2026-09-10): the model never writes free text (one guarded,
title-only exception — 2026-09-11, below). The two former free-text slots
become selection problems:

- **Summary** — becomes a multi-variant segmented paragraph: pre-written,
  `key-*`-tagged sentences the model (or the user) turns on/off, exactly
  like blocks. The hardcoded ".NET Core, Angular, SQL Server" lead
  disappears behind variants aligned with the enabled `keys`. The template
  renders the enabled segments from the ResumeContext.
- **JobTitle** — decided (2026-09-11): fixed pre-written headline variants
  selected by the enabled keys; copying the raw `job.Title` is dropped. One
  bounded exception: the model may *propose* free text for the title slot
  only. Pending-title mechanism (2026-09-17, replacing the earlier
  "acceptable because the review gate approves every delta" rationale): the
  proposal lives in an additive `Job.AiTitle` column (single line ≤ 80
  chars) and **never renders unaccepted** — until explicit acceptance the
  selected/default variant renders; accepting writes it into
  `AiOptions.JobTitle` and clears the column. Guards: (a) title only — the
  summary and every other slot stay strictly selection-only (multi-line
  prose carries higher fabrication risk and is harder to review); (b) the
  job-detail diff UI flags the pending title as free text with explicit
  accept/reject actions; (c) server-side validation enforces a single line
  ≤ 80 chars. Template details settle in the phase-3 build.

## 4. Guardrails (non-negotiable)

1. **Selection only.** The model chooses among pre-written blocks and
   summary/title variants. It never writes free text (the guarded
   title-only exception of §3 aside), and never adds experience entries,
   employers, dates, or metrics.
2. **Human in the loop — user duty on job-detail (2026-09-17).**
   *Supersedes the earlier rule 2 ("previewed on the dashboard and must be
   approved before a job moves to `Applied`") — decision log, "Phase-3
   ambiguities resolved".* There is no mechanical approval step for
   selection deltas: they are live immediately, and reviewing/adjusting
   them before going to the apply page is the user's duty, performed on the
   job-detail page — the only UI path to the resume (resume links exist
   nowhere else). The free-text title is the sole exception: it never
   renders unaccepted (§3).
3. **Versioned and reversible.** `Job.AiOptions` is stored separately from
   `Options`; deleting the delta falls back to the regex-built context.

## 5. If direct HTML surgery is ever needed

The safe variant, should block-level selection ever prove insufficient:

- The model emits whitelisted operations only —
  `hide(selector)` / `remove(selector)` / `setText(selector, text)` — and
  nothing else.
- The **server** applies them with HtmlAgilityPack (already a dependency;
  see `JobEligibilityHelper.GetTextContent`), never touching `<script>` or
  `<style>`, and validates the result.

This is strictly weaker than the delta pattern — it bypasses `Job.Options`
and the template's own show/hide logic — so treat it as a last resort.

## 6. Flow

```
Job (State = Attention)
   │
   ▼
ai-worker call 2 (AI_INTEGRATION.md §6; runs only when the verdict
     promotes to Attention — threshold = AiPassmark)
     input: rubric + block inventory + current context + JD
   │
   ▼
delta JSON  (keys / included & notIncluded selectors / length /
     summary-segment & title-variant selection)
   │
   ▼
POST /ai/verdict — the CORE validates the delta, applies it onto the
     regex-built Options and stores the complete context in Job.AiOptions
     (new column; Options kept as fallback; raw delta appended to job.Log)
   │
   ▼
job-detail page: field-level diff AiOptions vs Options — review and
     adjustment is the user's duty (no approval step; the only UI path
     to the resume)
   │
   ▼
resume view renders Options.HumanEdited ? Options : (AiOptions ?? Options)
   │
   ▼
/job/resume?jobid=...
   │
   ▼
user prints from the browser (Brave print dialog) — @media print CSS is in the template
```

Decided (2026-09-11) — human/AI coexistence, three layers:

- `ResumeContext` gains a `HumanEdited` flag inside the stored JSON (the
  `Version` constant bumps with it). The flag round-trips through
  `SimlpeSerialize`/`SimlpeDeserialize` with the rest of the context —
  `Job.Options` uses that "simple JSON" format, not standard JSON.
- Rendering precedence: `Options.HumanEdited ? Options : (AiOptions ??
  Options)` — a human edit always wins; otherwise the AI delta; otherwise
  the regex-built context.
- AI writes only `AiOptions`, never `Options`; human edits keep writing
  `Options` through the existing `JobController.Options` → `ChangeOptions`
  path. After a human edit, newer AI suggestions appear on the job-detail
  page as a diff with an explicit accept action; accepting is a **full
  copy** into `Options` (sets `HumanEdited`) — never a field-wise merge
  (2026-09-17). A re-run rewrites `AiOptions`, unless
  `Options.HumanEdited` — then the new suggestion stays a diff-only
  proposal.

`Applied` remains a manual user action; phase-5 integration is revisited in
the final phase.

### Delivery (decided)

The CloudConvert HTML→PDF path (`wwwroot/scripts/resume-pdf.js`) is retired —
its API key was removed and the service is no longer used. The print surface
is `/job/resume` in any browser: the template's `@media print` rules make the
served page print-ready, so printing to paper/PDF is a manual browser step.
No server-side PDF conversion and no new `print` browser command are planned
(see [`AI_DECISION_LOG.md`](AI_DECISION_LOG.md)).
