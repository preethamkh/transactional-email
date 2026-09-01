# Integration Guide — D365 & Power Automate (for the separate owners)

This is **guidance only** — nothing here is implemented, and no D365/Power Automate resource was touched. This describes what the responsible owners would configure to call the central email service. The central API is the single HTTP contract; D365 and Power Automate cannot reference a .NET library, so they call HTTP.

---

## The contract they call

```http
POST https://<service-url>/api/v1/email/send
X-Source-System: <powerautomate | d365>
X-Api-Key: <per-system-key>
Content-Type: application/json

{
  "templateKey": "AssessmentBooked",
  "to": [{ "email": "candidate@example.com", "name": "Jane Example" }],
  "data": {
    "candidate": { "id": "C-10045", "name": "Jane Example", "email": "candidate@example.com" },
    "assessment": { "type": "Capability Assessment", "status": "Booked" },
    "session": { "date": "Monday, 25 August 2026", "location": { "name": "Melbourne" } }
  },
  "sourceSystem": "powerautomate",
  "correlationId": "<guid>",
  "idempotencyKey": "<same-guid>"
}
```

Response (202 Accepted):

```json
{ "accepted": true, "status": "queued", "correlationId": "<guid>",
  "providerMessageId": "<mandrill-id>", "sourceSystem": "powerautomate",
  "templateKey": "AssessmentBooked" }
```

Store the returned `correlationId` for support tracing.

---

## Power Automate (flow owner)

1. Use the existing Dataverse trigger for the relevant assessment event (e.g. assessment created/status changed).
2. Add an **HTTP** action.
3. Method `POST`, URI to `/api/v1/email/send`.
4. Headers: `X-Source-System` = `powerautomate`; `X-Api-Key` = the per-flow key.
5. Body: map Dataverse fields into `templateKey`, `to`, `data` (nesting allowed as shown).
6. Store the returned correlation ID (e.g. in a field or log) for support.
7. Configure retry and failure notification per the logging-ownership model.
8. Keep the `X-Api-Key` in a **protected flow configuration** (or Key Vault reference), never in a visible action.

Notes:
- No Mandrill credentials in the flow when using the central API.
- If D365 must also be the system of record, add a write-back step (see below) after the HTTP call, or rely on the async webhook consumer.

---

## D365 (Dataverse owner)

### Sending
- **Option A — HTTP action in a flow:** same as Power Automate above, triggered by a Dataverse workflow/cloud flow.
- **Option B — Custom connector:** generate an OpenAPI document from the central API (`/openapi/v1.json`) and publish a custom connector; then use its actions in flows.

### Write-back (keep D365 as system of record, optional)
- If assessment emails must appear on the Contact timeline:
  1. Resolve recipient email → Contact (`emailaddress1`).
  2. Create/update an **Email activity** via the Dataverse Web API (`POST /emails`).
  3. Bind via `_regardingobjectid_contact@odata.bind` to place it on the timeline.
  4. Add parties: `participationtypemask 1` (sender, systemuser bind) and `participationtypemask 2` (To recipient).
  5. Store the provider/correlation ID so webhook retries don't duplicate entries.
- Use a **service principal with least-privilege** Email Create/Write + Contact Read.
- This write-back is best done asynchronously from the central service webhook consumer (a separate owner), so the send is not blocked by Dataverse API limits.

---

## What NOT to do

- Do **not** put Mandrill API keys in D365 or Power Automate.
- Do **not** have each flow/plugin call Mandrill directly — that recreates the coupling the central service removes.
- Do **not** rely on the provider's 30-day retention for audit; use the central audit record.

---

## Ownership & approval

These changes belong to the **D365/Power Automate owners**, not the .NET developer building the service. This document defines the contract and intended behaviour so the service can be built first and integrated later without rework.
