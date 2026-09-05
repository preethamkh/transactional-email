# Option 4 — Central Email Service (Recommended)

**Status:** Proposed — to be added to the options paper as the 4th option
**Date:** 22 August 2026
**Author:** Preetham (with senior-architect review)

---

## 1. The Core Insight

The options paper debates **which vendor** sends email (Mailchimp vs Dynamics Customer Insights vs "keep current"). That is the wrong primary question. Every option that passes validation has the same *shape*:

> **One HTTP endpoint that any system can call. One template store business users own. One activity log. One branding model. A swappable provider underneath.**

If organisation builds that shape, the vendor becomes a **configuration choice, not an architectural commitment**:

- Start with **SendGrid** (already the sender, already licensed, has a no-code template editor) — zero new licensing.
- If the business later insists on Mailchimp templates or Customer Insights journeys, swap the adapter. Callers don't change.

Options 1–3 each hard-wire a vendor and force organisation to bet today, with incomplete information (Mandrill not even provisioned, Customer Insights unpriced). Option 4 defers the bet until there is evidence.

## 2. What It Is

A deliberately **thin** ASP.NET Core Minimal API service (a few hundred lines, single deployable):

```
                    ┌─────────────────────────────────────────────┐
                    │        Central Email Service (thin)         │
                    │                                             │
 Assessment Portal ─┤  POST /api/v1/email/send                    │
 Accreditation Portal─► GET  /api/v1/templates/{key}/preview     │
 Power Automate  ───┤  GET  /api/v1/templates                     │
 D365 (via flow) ───┤                                             │
                    │  ┌───────────┐  ┌──────────┐  ┌──────────┐  │
                    │  │ Auth      │  │ Template │  │ Activity │  │
                    │  │ per-system│  │ registry │  │ log +    │  │
                    │  │ API keys  │  │ key → ID │  │ webhook  │  │
                    │  └───────────┘  │ + brand  │  │ receiver │  │
                    │                 └──────────┘  └──────────┘  │
                    │        ┌─────────────────────┐              │
                    │        │  IEmailProvider     │              │
                    │        │  ├─ SendGridProvider│  ◄── swap    │
                    │        │  └─ (Mandrill/CJ)   │      point  │
                    │        └─────────────────────┘              │
                    └─────────────────────────────────────────────┘
```

**Not** a microservices platform. **Not** a new product. One small service with four responsibilities: auth, template resolution, provider dispatch, activity logging.

### API contract (POC version)

```http
POST /api/v1/email/send
X-Api-Key: <per-system key>
{
  "templateKey": "PasswordReset",
  "to": [{ "email": "user@example.org", "name": "Jane" }],
  "data": { "firstName": "Jane", "resetLink": "https://..." },
  "branding": "demo",              // optional; default "demo"
  "sourceSystem": "assessment-portal",
  "idempotencyKey": "guid"        // optional, prevents duplicate sends
}
```

### Provider abstraction

```csharp
public interface IEmailProvider
{
    Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct);
}
```

The POC ships `SendGridProvider` (REST v3 + Dynamic Templates). A `MandrillProvider` can be added later using Track A findings — proving reversibility is itself a POC outcome.

### Template registry (POC: JSON → production: SQL)

| templateKey | provider | providerTemplateId | branding | owner |
|---|---|---|---|---|
| PasswordReset | sendgrid | d-abc123 | demo | Engagement |
| AccApproved | sendgrid | d-def456 | demo | Accreditation |

Business users edit templates **in the provider's editor**; the registry just maps stable keys to provider IDs. Renaming or re-versioning a template never breaks a caller.

## 3. How Business Users Manage Templates (No Technical Knowledge)

This is the question the paper keeps circling. The answer: **don't build an admin UI — reuse the provider's editor.**

- **SendGrid Design Editor** is genuinely no-code: drag-drop builder, `{{handlebars}}` merge fields with **test-data preview**, versioning, duplicate/rollback. Business users edit and preview; developers never touch content.
- **Hybrid if the team prefers Mailchimp's editor:** designers keep designing in Mailchimp, export the HTML, paste it into a SendGrid dynamic template. One template store, one send path — Mailchimp becomes a design tool, not a second system of record. (This gives the BA most of what she wants without the split-brain problem.)
- **Governance (draft → review → approved):** process-based at first (template owner reviews in the editor before activating a version). A thin approval UI can be added later **only if** the process proves insufficient. Don't build it speculatively.

## 4. Integration Playbook (Each System)

| System | How it calls the service | Effort |
|---|---|---|
| **Assessment Portal** (today's monolith) | **Facade trick:** implement SharedLib's `ISendGridService` interface backed by calls to the central API, then change **one line** — the DI registration at `Program.cs:139`. All ~20 controllers migrate instantly with zero call-site edits. Migrate call sites to a portal-owned `IEmailClient` gradually afterwards. | 1 interface + 1 line to cut over; gradual cleanup after |
| **Accreditation Portal** (new repo, Nov 2026) | References the same thin client package (`TransactionalEmail.Client`) or plain `HttpClient` — same contract. | Trivial by design |
| **Power Automate** | HTTP action → `POST /api/v1/email/send` with a per-flow API key. | Low |
| **D365** | D365-side triggers (workflows/plugins) fire a Power Automate flow that calls the service. (D365 cannot consume a .NET package — this is why the SA's NuGet-only idea fails FR-004.) | Low |
| **Mailchimp marketing** | Unchanged. Audience sync stays exactly as is. | None |

### The NuGet question (SA's proposal) — resolved, not rejected

The SA is right that shared logic should be packaged; he's wrong that a package alone suffices. D365 and Power Automate **cannot reference a .NET library** — they need HTTP. The resolution gives him his package:

- `TransactionalEmail.Client` — typed client for .NET callers (both portals)
- Central API — same core logic, exposed over HTTP for D365/Power Automate

Package and API are two skins over one small core. This also avoids repeating the SharedLib lock-in mistake: the package is organisation-owned, in organisation's feed, wrapping an organisation-owned service.

## 5. Why Not the Simpler Alternatives?

| Alternative | Why it fails |
|---|---|
| **Call SendGrid directly from every system** (no middle layer) | Violates FR-004: no single endpoint, no central logging/branding policy, every system re-implements retry/auth/tracking. The middle layer is ~300 lines — cheap insurance and the only place activity write-back to D365/portal can live consistently. |
| **NuGet package only (SA's idea)** | D365/Power Automate can't call it. Doesn't meet FR-004. |
| **Mailchimp as template store, portals keep sending (Option 2)** | Two template syntaxes (`*|MERGE|*` vs current `{Placeholder}`), a sync process that can silently drift, no activity tracking story, and per the email draft: Mandrill isn't even licensed yet. It's a process, not a capability. |
| **Customer Insights (Option 3)** | Right shape, wrong price/timing for transactional email. Revisit later **as a provider adapter** if the marketing-journey business case matures — nothing in Option 4 blocks it. |
| **Mailchimp for all delivery (Option 1)** | Rewrites 20+ call sites, adds write-back integrations the paper itself can't validate, and Mailchimp's transactional product (Mandrill) is a separate paid product. Highest cost, least reversible. |

## 6. Pros / Cons (Honest)

**Pros**
- Meets all BRD requirements (FR-001, 003, 004, 005, 007) — none of Options 1–2 do
- Zero new licensing for the POC and likely production (SendGrid already paid for)
- Vendor-reversible — the Mailchimp-vs-SendGrid debate becomes a swap, not a rewrite
- Business users get a real no-code editor on day one
- One-line cutover for the existing portal via the `ISendGridService` facade
- Serves both portals after the Accreditation separation
- Creates the activity-tracking foundation (webhooks → log → future D365 write-back) the paper lists as a goal but never plans

**Cons / Risks (with mitigations)**
- One more deployable to operate → start as an Azure Function (consumption ≈ $0 at this volume) or App Service slot; single service, no sprawl
- SendGrid editor is slightly less polished than Mailchimp's for marketers → hybrid design-in-Mailchimp/paste-into-SendGrid workflow
- New service needs tests → the POC branch starts the test project; the service is small enough to cover properly (the monolith has zero tests — don't replicate that)
- Deliverability depends on SPF/DKIM/DMARC → already sending via SendGrid in prod; verify domain auth status as a POC checkbox
- Governance workflow is manual at first → acceptable for 31 templates; add UI only if proven necessary

## 6b. Logging Model — Operational Log vs Customer History

Two distinct concerns; the POC implements the first and designs the second:

1. **Operational log** (built in the POC): every API operation — caller, template, status, latency — plus provider webhook events. JSONL/activity endpoint now, SQL later. Purpose: debugging, audit, FR-003 foundation.
2. **Customer-visible communication history** (production, Phase 2): what staff see on a **D365 Contact → Communication → Timeline** ("Communications Sent"). Current state already shows assessment emails there from legacy senders ("<Assessment>", "No Reply") with `{!User:First Name;}` merge syntax — evidence that D365-side templates and some flow-based logging exist today. Under Option 4 the central service becomes the *single* component performing this write-back: recipient email → contact match → create Email activity via Dataverse Web API (app registration/S2S), batched/async from the webhook consumer. Accreditation events route to the Accreditation Portal DB instead, per the agreed business decision.

So: the POC does not write to D365 (that needs an app registration + approval), but everything it logs is shaped to feed that write-back, and a thin spike can validate the Dataverse create-email contract before Phase 2.

### Spike result (22 Aug, PROD org `example.crm6` — see environment correction) — write-back contract VALIDATED

Created one draft Email activity via the Dataverse Web API against the contact `Preetham.KH@example.com`.

> **Environment correction:** the connected organisation reports display-name `org-uat`, but its URL is `https://example.crm6.dynamics.com`, confirmed by the user to be **production**. The assumption that an `org-uat`-style org name implied a sandbox was wrong — environment identity must always be verified from the URL. The user visually confirmed the timeline entry in their live Communication tab. Technically none of the findings change (draft creation is equally safe in either environment), but future CRM-side validation must target **DEV: `https://org-dev.crm6.dynamics.com`** (or have the MCP connection repointed) before any further writes.

| Contract element | Verified behaviour |
|---|---|
| Draft creation | `POST /emails` with no send invocation → `statecode 0 Open`, `statuscode 1 Draft`; nothing is emailed |
| Timeline placement | `_regardingobjectid_contact@odata.bind` → appears on the contact's Communication timeline |
| Parties | Inline `email_activity_parties` array works: mask 1 = Sender (systemuser bind), mask 2 = To recipient; Owner/Regarding parties auto-created |
| Recipient matching | Production webhook consumer resolves recipient email → contact (emailaddress1) before binding |

Production notes captured from the spike: (1) use the standard Web API create which returns the new activity id in the response header — needed for idempotency keyed on `central_message_id` (store in a custom string field or subject token) so webhook retries don't duplicate timeline entries; (2) avoid bracket characters when filtering subjects via OData `contains` (cost us one failed lookup); (3) an S2S app registration with Email Create privilege replaces interactive credentials. Test draft left in PROD labelled "[POC] … safe to delete" — deletion pending user confirmation; no other records were created or modified.

Context observed in UAT while verifying: live D365-generated emails ("Payment received", "READY FOR PROCESSING…", some stuck at statuscode 6 *Pending Send*) confirm flow-based email logging exists today and would be replaced/unified by this service.

## 7. Migration Roadmap (Post-POC, Indicative)

1. **Foundation (2–3 wks):** central service + SendGrid provider + registry + activity log + tests; facade `ISendGridService` implemented
2. **Cutover (1 wk):** swap DI registration; all existing emails flow through the service with old static templates still working (provider renders fallback HTML)
3. **Template migration (2–3 wks):** move 31 templates to SendGrid Dynamic Templates in priority order (identity/auth first, accreditation batch second); retire `wwwroot/Email` reads per template
4. **Adoption (parallel):** Power Automate flows move to the API; Accreditation Portal repo starts on `TransactionalEmail.Client` from day one
5. **Later (optional):** D365 activity write-back, approval workflow UI, additional provider adapters

## 8. What This POC Must Prove

1. A business user can edit a template in SendGrid's editor and the change is live with **no deployment** ← the money shot
2. One endpoint serves a .NET caller, an HTTP caller, and a Power Automate-shaped call identically
3. Send + failure logging works and is queryable
4. The provider seam is real (document what a Mandrill adapter would need, from Track A findings)
5. Pricing: total incremental cost vs Options 1–3 (expected: ~$0 incremental)

*POC execution detail: see [02-poc-strategy-three-tracks.md](02-poc-strategy-three-tracks.md).*
