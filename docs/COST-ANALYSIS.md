# Cost Analysis: Email Architecture Demo & Production Estimate

This document separates **demo cost** (what you might pay to prove the point) from **production cost** (what the real feature would cost). All Azure references are to the **personal `bonny_kh@hotmail.com` subscription** — nothing here touches the organization `Pay-As-You-Go` subscription.

> Prices are indicative and change. Always confirm with the [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/) and set a budget alert before any paid resource.

---

## 1. What already exists on the personal subscription (inspection result)

| Resource group | Resource | SKU/tier | Notes |
|---|---|---|---|
| `rg-preetham-lab` | `ASP-rgpreethamlab-b0b4` (App Service plan) | **F1 (Free)** | Reusable for a hosted demo |
| `rg-preetham-lab` | `preetham-lab-web` (App Service) | F1 | Existing demo site |
| `rg-preetham-lab` | `preethamlabstorage01` (Storage) | Standard_LRS (RA-GRS shown) | Reusable |
| `rg-preetham-lab` | App Insights components | Free tier limits | Existing |
| `rg-preetham-terraform` | Storage + App Service + App Insights | F1 + standard storage | Terraform lab already present |

**Implication:** For a **free-tier hosted demo** you may not need to create anything new — you could reuse the existing F1 plan/app. The new Terraform creates a separate, isolated resource group (`rg-email-architecture-demo`) so the demo is clean and removable.

---

## 2. Demo cost

### Option A — Fully local (recommended, $0)
- Runs on your machine. No Azure resources created.
- Proves: HTTP contract, audit UI, nested data, auth, shared-library alternative.
- **Cost: $0.**

### Option B — Hosted on free tier (≈ $0)
- Apply the Terraform in `infra/` to the **personal** subscription: F1 Linux App Service + Standard LRS storage.
- F1 is free. Storage LRS is a few $/mo for negligible usage (single-digit dollars, often cents at demo volume).
- **Caution:** F1 plans are `Shared` compute — **Always On is disabled** and apps can sleep. A presentation could hit a cold-start delay. Not ideal for production, fine for a demo.
- **Cost: ≈ $0–low single digits / mo.**

### Option C — Add Function App + Service Bus + SQL for "full" demo
- These are **not free**: Azure SQL, Service Bus Standard, and even Function App consumption have associated costs (Functions consumption is cheap but not $0 at meaningful volume; SQL Basic and Service Bus Basic exist but are paid).
- **Not required to make the architectural point.** Do not provision for the demo unless specifically asked.

**Recommendation: Option A (local) for the decision meeting.** Option B only if you specifically want a public URL.

---

## 3. Production cost estimate (indicative)

These are rough orders of magnitude for the real feature, assuming Mandrill is the chosen provider and the organization hosts in Azure. Confirm with Pricing Calculator + your volume.

| Resource | Purpose | Indicative tier | Cost driver |
|---|---|---|---|
| App Service (or Container App) | Host central email API + support UI | P1v3 / B1–B2 | Always On, SLA, scale |
| Azure Functions (consumption) | Webhook receiver, audit archive, D365 write-back | Consumption / Flex | Requests & executions |
| Azure Service Bus | Queue webhook events, retry, DLQ | Standard | Messaging ops |
| Azure SQL | Hot audit store (90 days) | S0–S1 / Serverless | vCores/DTUs, storage |
| Blob Storage | Long-term archive (7 yr) | Cool/Cold | GB stored |
| Application Insights | Monitoring, traces, alerts | Pay-as-you-go | GB ingested |
| Log Analytics | Query/retention for audit analytics | Pay-as-you-go | GB ingested |
| Microsoft Entra ID | Staff login to support UI | Free tier (P1 if MFA/conditional access needed) | Users |
| Key Vault | Secrets (Mandrill key) | Standard | Transactions |

**Key production considerations (bigger than line items):**
- **Mandrill is a separate paid cost** from Azure. Confirm transactional volume/tier.
- **Always On** is required for the API → you generally need a paid App Service tier (F1 can't do Always On).
- **Retention policy** (30-day hot vs 7-year archive) drives most storage cost.
- **PII/privacy**: storing email bodies is a data-retention and access-control decision, not just a cost one.
- **D365 write-back** uses Dataverse API — limited by API request quotas/licensing, not Azure compute.
- **APIM** was deliberately excluded from the baseline; add only if external consumers/quotas/policies are required.

---

## 4. Separate DB vs. existing organization DB — recommendation

**Recommendation: a separate email/audit database**, not new tables in the existing organization application DB.

| Consideration | Same organization DB | Separate DB |
|---|---|---|
| Coupling | Couples service to organization schema/ownership | Independent ownership |
| Blast radius | Schema changes can affect other features | Isolated |
| Demo/rollback | Touches shared DB | Safe to destroy |
| Reporting | Single DB to query | Cross-DB query needed |
| Cost | Slightly less | Slightly more |

A separate database (or a dedicated schema with clear ownership if DB sprawl is a concern) is the cleaner long-term choice, especially given the Accreditation Portal separation.

---

## 5. Summary for the meeting

- **To prove the architecture decision:** local demo, **$0**, enough evidence.
- **To add a public URL:** free-tier F1, ≈ $0, but F1 sleeps (not production-grade).
- **To run in production:** expect costs for App Service (paid tier), Functions, Service Bus, SQL, Blob, App Insights — plus **Mandrill** fees. Exact numbers need your volume and a Pricing Calculator run.
- **Never** provision anything against the organization `Pay-As-You-Go` subscription for this demo.
