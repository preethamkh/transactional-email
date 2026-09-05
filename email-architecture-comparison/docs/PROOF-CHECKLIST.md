# Proof Checklist for the Meeting

This document prevents the demonstration from overstating what a small POC proves.

## What the POC proves

| Claim | Evidence |
|---|---|
| The central service can expose one HTTP contract | `demo.http` sends requests to `POST /api/v1/email/send` |
| Different callers can use the same contract | Run the assessment and accreditation requests with different `sourceSystem` values |
| Rich domain data can be accepted | The request contains candidate, assessment, session and location objects |
| Provider mapping can handle rich data | `MandrillEmailSender` flattens nested values to provider-safe names such as `session_location_name` |
| Mandrill template sending is technically viable | Set a test Mandrill key and use an existing authenticated test template/recipient |
| Audit data can outlive provider retention | The API stores the send result locally and displays it in the support UI |
| Support can search by key fields | Search the UI by recipient, template or correlation ID |
| Central and distributed shapes are different | Run the API path and the direct shared-library console path |
| D365/Power Automate need HTTP | The integration guide contains their HTTP contract; no D365/PA package reference is required |

## What the POC does not prove

- It is not a production-ready email platform.
- It does not prove a business user's Mandrill template editor workflow; that requires a real Mandrill account/template and a live send.
- It does not prove arbitrary nested JSON can be referenced directly by Mandrill. The adapter must map domain data to template variables.
- It does not prove durable SQL/Blob retention; the demo uses in-memory audit data.
- It does not prove Service Bus delivery or Azure Function processing; those projects are deployable artifacts, while the local API uses a simple in-process path.
- It does not implement D365 or Power Automate changes.
- It does not validate production volumes, deliverability, support SLAs or licensing.

## How to answer “this is only a demo”

Use this response:

> “Yes, it is a deliberately small POC. It is not intended to prove the whole production system. It proves the architectural seams we need to decide on: an HTTP contract, a direct-library alternative, Mandrill provider mapping, nested business data mapping, correlation IDs, audit search and caller isolation. The production plan separately identifies SQL, Service Bus, Functions, identity, alerting and failure testing.”

If challenged further, run the provider-backed test with a non-production Mandrill template and recipient. That proves the external boundary; the local simulation proves repeatability without risking a live send during the meeting.

## Minimum evidence to capture

1. `dotnet build` succeeds.
2. `GET /health` returns `mode: simulation` or `mode: live`.
3. Nested send returns `202` and a correlation ID.
4. Support UI displays the audit row.
5. Missing key returns `401`.
6. Shared-library console demonstrates the alternative path.
7. If using live Mandrill, record the provider status/message ID and redact all credentials.
