# Transactional Email POC

Early proof-of-concept code that validated the two candidate options before the architecture was finalised:

| Folder | What it validates | Start here |
|---|---|---|
| [`email-option2-mailchimp/`](email-option2-mailchimp/README.md) | **Option 2 — provider template retrieval.** Mailchimp Marketing API template HTML fetching plus SendGrid/Mandrill send paths, runnable from the CLI. | `README.md` |
| [`email-option4-central-service/`](email-option4-central-service/README.md) | **Option 4 — central email service.** A small ASP.NET Core API with authentication middleware, template registry, provider seam (SendGrid), activity log, renderer and tests. | `README.md` |
| [`demo.http`](demo.http) | Shared REST Client script for both POCs. | — |

These were separate working branches merged into this folder with their commit history preserved. The findings from both
POCs feed the recommendation documented in `../email-docs/01-option-4-central-email-service.md`.