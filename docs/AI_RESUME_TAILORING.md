# AI resume tailoring

> **Status: design proposal — not implemented.** Companion to
> [`AI_INTEGRATION.md`](AI_INTEGRATION.md) (phase 3). Records the design for
> LLM-driven, per-job resume customization. No code exists yet.

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

## 2. The pattern: the LLM produces a ResumeContext *delta*, not HTML

Do not put the model on the raw template (a large single file with inline
JS/CSS — a quantized model will mangle it, and diffs become unreviewable).
Instead the model receives the job description plus an inventory of the
available blocks, and emits a small, reviewable JSON delta:

```
job description (plain text)
+ block inventory (element ids + key-* classes)
+ current ResumeContext
+ candidate profile (fixed system prompt)
                ▼  Qwen3, JSON-schema-constrained
{
  "keys":        ["JAVA", "SQL"],
  "notIncluded": ["#system-group-total", ".key-front-end",
                   "#web-sites li:nth-child(2)"],
  "length":      2,
  "summary":     "Backend-focused Java developer with 14 years ...
                  (rewritten to mirror the JD's vocabulary)",
  "jobTitle":    "Senior Java Backend Developer"
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

## 3. The only free-text slots worth generating

Structure should stay deterministic; two slots are legitimately free text:

- **Summary** — currently a hardcoded paragraph that always leads with
  ".NET Core, Angular, SQL Server", which is the wrong emphasis for a Java
  role. The template needs a one-line change to render a
  `Model.Summary` override. Hard constraint: the generated summary may only
  name stacks enabled in the delta's `keys`.
- **JobTitle** — `EvaluateEligibility` currently copies the raw
  `job.Title` ("Senior Java Developer (m/f/d) – Hybrid Berlin!"). The model
  normalizes it to a clean headline.

## 4. Guardrails (non-negotiable)

1. **Selection only.** The model chooses among pre-written blocks and rewrites
   the two free-text slots. It never adds experience entries, employers,
   dates, or metrics.
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
background AI worker (see AI_INTEGRATION.md §1)
   prompt: JD + block inventory + profile
   │
   ▼
delta JSON  (keys / removals / summary / title)
   │
   ▼
Job.AiOptions  (new column; Options kept as fallback)
   │
   ▼
dashboard: preview + human approval
   │
   ▼
resume view renders AiOptions ?? Options
```
