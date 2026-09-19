# Glossary

English terms used in the AI / memory design. Identifiers in code stay as
written here. Companion chat translations: [`GLOSSARY.fa.md`](../GLOSSARY.fa.md).

| Term | Meaning |
|------|---------|
| **Memory row** | One durable lesson in the unified memory table. `Scope` is `resume`, `apply`, or `ranking`. |
| **Confirmed** | The user has accepted the row. Unconfirmed rows are stored and never injected into prompts or used as fill values from `memory_query`. |
| **Active memory** | Confirmed rows eligible for prompts. The `memorycap` AppSetting (default 500) limits how many of these are **injected**, not how many rows exist. |
| **Distillation** | The user merging or deleting memory rows (phase-6 dashboard) so active memory fits the injection cap. Not a job that compresses ranking clicks into lessons. |
| **Pre-injection** | Deterministic labeled data block of active rows in a prompt (precedence order, token-capped, stable for prefix cache). Memory is data, never instructions. |
| **Hybrid retrieval** | Pre-injection plus `memory_query` for exploration. Query results used for fill are confirmed-only. |

A dedicated ranking **override log** (raw `jobId` / `AiScore` / user-action
events, later compressed into `Scope = ranking`) is **superseded**
(2026-09-19). Ranking learns from confirmed `ranking` rows only. Per-job
audit stays on `job.Log` and is not injected.
