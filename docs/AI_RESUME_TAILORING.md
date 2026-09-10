# AI resume tailoring

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 3). Records the design for
> LLM-driven, per-job resume customization, including the decided delivery
> step (§6): the tailored resume is printed manually from the browser, and
> the decided selection-only summary (§3). No AI code exists yet.

## 1. How the resume works today (the part that matters)

`Views/resume.cshtml` is not one document. It is a **superset** of every
possible resume variant, and the delivered resume is produced by pruning it:

- Every block is tagged with `key-*` classes (`key-dotnet`, `key-java`,
  `key-extra-2`, `key-not-extra-3`, ...), including English (`ltr`) and
  Persian (`rtl`) variants of each experience.
- Scoring already populates a `ResumeContext` (stored as `Job.Options`):
  `Keys`, `Included`/`NotIncluded`, `Length`, `Elements`, `INPUT_DATA`,
  `MORE`, `PageBreak`.
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
`AiOptions ?? Options`, and the block inventory for the prompt is derived
from the template automatically at runtime.

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

The delta is applied to a **separate** `Job.AiOptions` field; rendering and
template stay unchanged. The resume view resolves `AiOptions ?? Options`, so
the regex-built context always remains the fallback.

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
| `MORE` | Extra skill chips injected after the matching `key-*` chip |
| `INPUT_DATA` | Numeric overrides applied to elements carrying the matching class (e.g. years of experience) |
| `Elements` | Header items (phone, location, image, footer) |

## 3. No generated text — the summary becomes segmented

Decided (2026-09-10): the model never writes free text. The two former
free-text slots become selection problems:

- **Summary** — becomes a multi-variant segmented paragraph: pre-written,
  `key-*`-tagged sentences the model (or the user) turns on/off, exactly
  like blocks. The hardcoded ".NET Core, Angular, SQL Server" lead
  disappears behind variants aligned with the enabled `keys`. The template
  renders the enabled segments from the ResumeContext.
- **JobTitle** — same treatment intended (fixed headline variants instead of
  copying the raw `job.Title`); final call open (round 2).

## 4. Guardrails (non-negotiable)

1. **Selection only.** The model chooses among pre-written blocks and
   summary/title variants. It never writes free text, and never adds
   experience entries, employers, dates, or metrics.
2. **Human in the loop.** The delta is previewed on the dashboard and must be
   approved before a job moves to `Applied`.
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
background AI worker (see AI_INTEGRATION.md §2.1)
    prompt: JD + block inventory + profile
    (produced during manual ai-worker runs)
   │
   ▼
delta JSON  (keys / removals / summary segments / title variant)
   │
   ▼
Job.AiOptions  (new column; Options kept as fallback)
   │
   ▼
dashboard: preview + human approval
   │
   ▼
resume view renders AiOptions ?? Options   →   /job/resume?jobid=...
   │
   ▼
user prints from the browser (Brave print dialog) — @media print CSS is in the template
```

Open (round 2): how AI deltas and human edits coexist
(`ResumeContext.Version` interplay), and whether `jobTitle` gets fixed
variants like the summary. `Applied` remains a manual user action;
phase-5 integration is revisited in the final phase.

### Delivery (decided)

The CloudConvert HTML→PDF path (`wwwroot/scripts/resume-pdf.js`) is retired —
its API key was removed and the service is no longer used. The print surface
is `/job/resume` in any browser: the template's `@media print` rules make the
served page print-ready, so printing to paper/PDF is a manual browser step.
No server-side PDF conversion and no new `print` browser command are planned
(see the decision log in [`AI_INTEGRATION.md`](AI_INTEGRATION.md) §8).
