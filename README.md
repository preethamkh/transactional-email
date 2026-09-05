# Transactional Email

A small, standalone proof-of-concept workspace that explores transactional email architecture options.
This repository is **independent and vendor-neutral** — it was extracted from a larger codebase and contains no
references to that codebase, its teams, or its infrastructure. It is a personal design/learning space, kept free to be
re-used or re-integrated into any host workspace if needed.

## Repository layout

| Folder | What it is | Where to start |
|---|---|---|
| [`email-architecture-comparison/`](email-architecture-comparison/README.md) | The main demo: shared-library (distributed) sending vs a **central email service**, with a thin client, audit/support view, Mandrill adapter and Terraform for an optional demo deployment | its own `README.md` |
| [`email-docs/`](email-docs/README.md) | Analysis and design documentation: options paper, central-service recommendation, POC strategy, manager ask, run/demo guide, D365 & Power Automate integration notes | its own `README.md` |
| [`transactional-email-poc/`](transactional-email-poc/README.md) | Early provider POC code: Mailchimp template retrieval (Option 2) and the central email service spike (Option 4), plus `demo.http` | `email-option2-mailchimp/README.md` and `email-option4-central-service/README.md` |

> **Reading order tip:** start with [`email-docs/README.md`](email-docs/README.md) for the “why”, then
> [`email-architecture-comparison/README.md`](email-architecture-comparison/README.md) for the “how”.

## Quick start — main demo

```powershell
# from the repository root
cd email-architecture-comparison

dotnet build EmailArchitectureComparison.slnx
dotnet run --project src/TransactionalEmail.CentralApi
```

- The API runs in **simulation mode** unless `MANDRILL_API_KEY` is set, so nothing is sent by default.
- Open `demo.http` in VS Code with the REST Client extension and run the numbered requests against `http://localhost:5080`.
- Open `http://localhost:5080/` for the support/audit view.
- For a real send, set `MANDRILL_API_KEY`, `FROM_EMAIL` and `DEMO_TO_EMAIL` as **environment variables only** (never commit secrets).

## History & integrity

This repository preserves the **original commit history, dates, messages and authorship** of the extracted work.
Each folder was brought in with `git subtree`, so every section keeps its own commit history and no squash was applied.

## Re-integrating into another workspace

If this POC ever needs to be placed back inside a parent solution/workspace, see
[`docs/REINTEGRATION-GUIDE.md`](docs/REINTEGRATION-GUIDE.md). It contains both step-by-step manual instructions and a
ready-to-use agent prompt.