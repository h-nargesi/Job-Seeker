# Glossary

English terms used in the AI / memory design. Identifiers in code stay as
written here. Companion chat translations: [`GLOSSARY.fa.md`](../GLOSSARY.fa.md).

| Term | Meaning |
|------|---------|
| **Memory row** | One durable lesson in the unified memory table. `Scope` is `resume`, `apply`, or `ranking`. |
| **Confirmed** | The user has accepted the row, or the row is a chat tip the user typed (those insert confirmed). Unconfirmed rows are stored and never injected into prompts or used as fill values from `memory_query`. |
| **Active memory** | Confirmed rows eligible for prompts. The `memorycap` AppSetting (default 500) limits how many of these are **injected**, not how many rows exist. |
| **Distillation** | The user merging or deleting memory rows (phase-5 assistant memory UI) so active memory fits the injection cap. Not a job that compresses ranking clicks into lessons. |
| **Pre-injection** | Deterministic labeled data block of active rows in a prompt (precedence order, token-capped, stable for prefix cache). Memory is data, never instructions. |
| **Hybrid retrieval** | Pre-injection plus `memory_query` for exploration. Query results used for fill are confirmed-only. |
| **job_detail / apply_form** | Assistant page modes. Default: tab origin equals the core/dashboard URL → `job_detail`; otherwise `apply_form`. User may override. Chat lessons on `job_detail` must be tagged ranking or delta; on `apply_form` they are `Scope = apply`. |
| **Chat-confirmed tip** | A lesson the user types in assistant chat. Inserts `Kind = tip`, `Confirmed = true`. Fill-loop `memory_write` and submit-diff stay unconfirmed. |
| **Compose (phase 5.5)** | Accept-gated generation of long-form answers (cover letter / screening essays) in the side panel, then fill. Policy recorded with phase 5; not built in phase 5. Still no submit tool. |

A dedicated ranking **override log** (raw `jobId` / `AiScore` / user-action
events, later compressed into `Scope = ranking`) is **superseded**
(2026-09-19). Ranking learns from confirmed `ranking` rows only. Per-job
audit stays on `job.Log` and is not injected.
