# Chat 16 — Reports Phase 7: Wrap-up

**Status:** done 2026-09-27. **Depends on:** Chat 15 (phase 6). Implements
[`DASHBOARD_CHARTS.md`](../DASHBOARD_CHARTS.md) §6 roadmap step 7 — the final
step of the reports implementation.

## Goal

Close out the reports project: flip the design doc's status header to
implemented, document the three stats endpoints in `docs/API.md`, tick the
`TODO.md` checkbox («پیاده‌سازی گزارشها»), and refresh the stale
AGENTS.md pointer row. Docs only — no code changes.

## Files

- `docs/DASHBOARD_CHARTS.md` — status header now reads "implemented
  2026-09-27 (Chats 10–16) … design record"; roadmap step 7 marked done
  (Chat 16). §1–§5 body left untouched (it is the design record).
- `docs/API.md` — `ReportController` section: intro admits the two JSON
  endpoints, `GET /report/stats` added to the view table, and a new
  "Stats JSON" subsection documents `GET /report/statsdaily` (D1–D3, 30-day
  window, 15 s dashboard poll) and `GET /report/statsfull` (S1–S10,
  `agencies`/`countries` filters; S1 Agencies-only caveat) with their
  camelCase response shapes as mapped from `StatsDailyResponse` /
  `StatsFullResponse`.
- `TODO.md` — «پیاده‌سازی گزارشها» ticked `[×]`.
- `AGENTS.md` — common-tasks row "Dashboard charts / stats page" refreshed
  from "(planned) / design only, not implemented" to implemented
  (D1–D3 + `/report/stats` S1–S10).
- `docs/impl/CHAT-16.md` — this file.

## Decisions recorded

- API.md documents the two JSON endpoints **inside** the existing
  `ReportController` section (a "Stats JSON" subsection) rather than a new
  top-level family — they share the dashboard role/auth and the
  `/report/*` prefix.
- Response shapes are described at member level (one member per chart,
  camelCase per the default ASP.NET Core serializer — no custom naming
  policy in `Program.cs`), pointing to `DASHBOARD_CHARTS.md` §4 for the
  per-chart semantics instead of duplicating them.
- AGENTS.md row update is wrap-up-adjacent but required for truthfulness:
  it still claimed the feature was design-only.

## Tests

Docs-only change — no test surface. Build not affected; no C# touched.

## Must not

No code, SQL, view, or auth changes; no DASHBOARD_CHARTS §1–§5 rewrites
(design record stays as written); no archive moves (TODO still has open
items).

## Validate

Review the four edited files render correctly; `GET /report/statsdaily`
and `/report/statsfull` behave as documented (smoke via the running server
if desired).
