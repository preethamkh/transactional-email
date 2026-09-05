# Transactional Email

A small, standalone proof-of-concept workspace that explores transactional email architecture options.
This repository is **independent and vendor-neutral**. It is a personal design/learning space, kept free to be
re-used or re-integrated into any host workspace if needed.

## Repository layout

| Folder | What it is | Where to start |
|---|---|---|
| [`email-architecture-comparison/`](email-architecture-comparison/README.md) | The main demo: shared-library (distributed) sending vs a **central email service**, with a thin client, audit/support view, Mandrill adapter and Terraform for an optional demo deployment | its own `README.md` |
| [`email-docs/`](email-docs/README.md) | Analysis and design documentation: options paper, central-service recommendation, POC strategy, run/demo guide and integration notes | its own `README.md` |
| [`transactional-email-poc/`](transactional-email-poc/README.md) | Early provider POC code: Mailchimp template retrieval (Option 2) and the central email service spike (Option 4), plus `demo.http` | `email-option2-mailchimp/README.md` and `email-option4-central-service/README.md` |

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

## Re-integrating into another workspace

If this POC needs to be placed inside a parent solution/workspace of a larger solution, see
[`docs/REINTEGRATION-GUIDE.md`](docs/REINTEGRATION-GUIDE.md). It contains both step-by-step manual instructions and a
ready-to-use agent prompt.
