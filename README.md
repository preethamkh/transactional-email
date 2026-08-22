# Transactional Email Capability — Working Folder

All analysis, POC planning and decision material for the Transactional Email Capability project lives here. Nothing in this folder has been committed to git yet (all files are untracked / local-only).

## TL;DR — Recommendation

**Add a 4th option to the paper: a thin Central Email Service** — one HTTP API endpoint, one template store, one activity log, with the email provider (SendGrid today, Mailchimp/Mandrill if ever justified) hidden behind a swappable adapter.

This is the only option that satisfies every BRD requirement (FR-001 templates, FR-003 API sending + reporting, FR-004 in-house shared endpoint, FR-005 migration of existing emails, FR-007 multi-branding) while keeping the vendor decision **reversible** instead of a bet.

The three POC tracks below don't replace the BA's requested POC — they complete it, and produce evidence the options paper is currently missing.

## Reading Order

| # | Document | Purpose |
|---|---|---|
| 1 | [01-option-4-central-email-service.md](01-option-4-central-email-service.md) | **The recommendation.** Architecture, API contract, integration playbook per system, business-user story, pros/cons, migration plan. |
| 2 | [02-poc-strategy.md](02-poc-strategy.md) | **The POC plan.** Why only two POC branches (Option 2 + Option 4), scorecard, demo script, pricing capture, git rules. |
| 3 | [03-manager-ask-and-timeline.md](03-manager-ask-and-timeline.md) | **What to ask for.** Access list incl. copy-paste Monday messages, time ask, success criteria to lock, meeting talking points. |
| 4 | [04-poc-run-and-demo-guide.md](04-poc-run-and-demo-guide.md) | **How to run everything.** Setup commands, user-secrets keys, build/run/test steps, demo walkthroughs, troubleshooting. |

> These files are committed on the local-only branch `poc/email-docs`. `master` stays clean.
> Run `git checkout poc/email-docs` to see them; `git checkout master` to switch back.

## Source Material (inputs — read-only)

Located in [`source/`](source/):

- `Transactional Email Capability - Options Paper.docx` — BA's options paper (Options 1–3)
- `capability-analysis.md` — prior senior-architect analysis; corrected the paper's factual errors (SendGrid is the actual sender, accreditation data is in D365, templates are static files in `wwwroot/Email/`, ShareIt.Library dependency, BRD requirements FR-001–FR-007)
- `poc-plan-draft.md` — earlier 1.5-day POC draft (superseded by doc 02)
- `email-draft-to-ba.md` — draft email flagging the Mandrill licensing gap

## Verified Current State (code-checked 22 Aug 2026)

- 31 static HTML/TXT templates in `PhysioPortal/wwwroot/Email/`, read via `Util.OpenEmailTemplate()` — a code deploy is needed for every wording change (the real problem)
- `ISendGridService` (ShareIt.Library.SendGrid) is injected in ~20+ controllers/utils; single DI registration at `PhysioPortal/Program.cs:139` — one swap point for migration
- Mailchimp integration is audience/marketing sync only (`MailchimpUtil.cs`, `MailChimp.Net.V3`), including a transactional audience — no transactional sending
- Accreditation + Assessment are areas of one monolith today; Accreditation repo separation targeted Nov 2026 — both portals will need the same email service afterwards

## Status

- [x] Analysis of options paper vs actual architecture
- [x] Option 4 design documented
- [x] Two-branch POC strategy + comparison framework
- [x] POC branches scaffolded, building, tested (local-only)
- [ ] Accounts/access requested (SendGrid scoped key, Mailchimp trial, Mandrill decision)
- [ ] POCs executed against live accounts, comparison demo prepared

## Addendum (22 Aug 2026) — Accreditation repo findings

The Accreditation repo (`C:\Dev\Accreditation`, docs pack in its `docs/` folder) was code-verified:

- **Zero D365 references.** The separated portal is SQL Server/EF Core (115 migrations), Auth0, ShareIt.Library. The claim in `source/capability-analysis.md` §2.2 that "accreditation data IS in D365" describes the **legacy monolith only** and must not be used in the meeting — for the new portal the paper's "accreditation insight stays in the Portal" premise is literally true.
- **Email sending is registered but unused** (`ISendGridService` at `Program.cs:70`, all consumers commented out). No template store migrated. The new portal is the **cleanest first adopter** of Option 4 — nothing to migrate.
- `EmailCatalogue` is a per-association email *address* directory (autocomplete), not a template store — don't conflate it with template governance.

Net effect on the decision: **strengthens Option 4** (serves both portals, no accreditation migration cost, aligns with the Portal-as-system-of-record decision).
