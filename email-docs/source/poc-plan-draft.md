# Transactional Email POC Plan — 1.5 Day Sprint

**Author:** Senior Solutions Architect  
**Date:** 21 August 2026  
**Context:** POC task before next catch-up meeting (3 days from now, ~1.5 days available)

---

## 1. POC Objective

Validate the feasibility of using **Mailchimp as a central template repository** with an **API to retrieve templates** and **send transactional emails**, comparing against the current SendGrid approach.

**BA's stated requirement:**
> "test sending emails, reporting, logging (compare with SendGrid), and getting templates out of MailChimp for D365 and Portal"

**SA's scoped-down POC:**
> "poc - create a dummy template in mailchimp, api to download that template in a string field, logging"

---

## 2. What We're Actually Testing (and What We're Not)

### In scope (1.5 days)
| # | Test | Why | Effort |
|---|---|---|---|
| 1 | **Create a dummy template in Mailchimp** | Validate template creation + storage | 0.5 hr |
| 2 | **API to download template into a string field** | Validate Mailchimp API can retrieve template HTML | 1-2 hrs |
| 3 | **Send a test email using the retrieved template** | Validate end-to-end send path | 1-2 hrs |
| 4 | **Basic logging** | Validate we can log send success/failure | 1 hr |
| 5 | **Compare with SendGrid** | Document differences (template mgmt, API, reporting, cost) | 1 hr |

### Out of scope (for this POC)
- D365 integration (the BA said "I don't think we're doing the D365 and Portal side of things")
- Portal integration (same)
- Power Automate integration
- Full template migration (31 templates)
- Reporting dashboard
- Approval workflows
- Multi-branding

### Can D365/Portal be done as a POC anyway?
**Yes, partially, but not needed for this sprint.** The Mailchimp API is the same regardless of caller. If the POC proves the API works (download template + send email), then D365 and Portal integration is just a matter of calling the same API from those systems. **The POC should focus on proving the Mailchimp API works, not on wiring up every consumer.**

---

## 3. Recommended POC Approach (SA's suggestion, refined)

The SA's suggestion is sound and pragmatic for a 1.5-day POC. Here's the refined plan:

### Step 1: Get a Mailchimp Trial Account (0.5 hr)
- Sign up for a **Mailchimp free/trial account** (no credit card needed for trial)
- Note: Mailchimp's **transactional email (Mandrill)** is a **separate product** with its own account. For the POC, we only need the **standard Mailchimp account** to create templates and use the API.
- **Important:** The free tier may have API limitations. If the trial doesn't include API access, we may need a paid plan or use the Mandrill trial.

### Step 2: Create a Dummy Template in Mailchimp (0.5 hr)
- Log into Mailchimp
- Create a simple HTML template (e.g., "POC Test Template")
- Add a merge field (e.g., `*|FNAME|*` for first name) to test dynamic content
- Note the template ID

### Step 3: Build a Minimal .NET Console App / Script to Test the API (2-3 hrs)
Create a small .NET console app (or use the existing `MailChimp.Net.V3` package already in the portal) that:

1. **Authenticates** with the Mailchimp API key
2. **Lists templates** — `GET /3.0/templates`
3. **Gets a specific template** — `GET /3.0/templates/{template_id}` — returns the HTML in a string field
4. **Sends a test email** using the template content (via Mailchimp's transactional API or by rendering the HTML and sending via SendGrid for comparison)

**Key API endpoints to test:**
```
GET /3.0/templates                    # List templates
GET /3.0/templates/{template_id}      # Get template HTML (returns "html" field as string)
POST /3.0/messages/send               # Send transactional email (Mandrill API)
```

### Step 4: Test Logging (1 hr)
- Log the API call results (success/failure, template ID, timestamp, recipient)
- Log to console + optionally to a simple SQL table or file
- This validates the "logging" requirement

### Step 5: Compare with SendGrid (1 hr)
Document the comparison:

| Aspect | SendGrid (current) | Mailchimp (proposed) |
|---|---|---|
| **Template management** | Dynamic Templates API | Templates API |
| **Template retrieval** | `GET /v3/templates/{id}` | `GET /3.0/templates/{id}` |
| **Transactional sending** | `POST /v3/mail/send` | Mandrill `POST /messages/send` |
| **Reporting** | Event webhooks + stats API | Activity feed + reports API |
| **Logging** | Event webhooks | Activity feed |
| **Cost** | Current licence | New licence (trial → paid) |
| **API key** | In Key Vault | In Key Vault |

---

## 4. The SA's NuGet Package Idea — Assessment

The SA suggested:
> "we could have it as a nuget package and this could live inside the portal and then this api could be made use of by d365 and power automate at a later stage"

### Assessment
**This is a reasonable idea, but with caveats:**

**Pros:**
- A NuGet package (e.g., `APC.EmailService`) would encapsulate the Mailchimp/SendGrid API calls
- Both the Assessment Portal and the future Accreditation Portal could reference it
- D365 and Power Automate could call the same package's API (if exposed as an HTTP endpoint)
- Reduces duplication across systems

**Cons / Risks:**
- **The ShareIt.Library precedent is a warning**: The org is already locked into ShareIt's private NuGet packages. Creating another private NuGet package (APC.EmailService) risks the same dependency problem if not properly governed.
- **A NuGet package alone doesn't solve the "shared API" requirement** (BRD FR-004). A NuGet package is a **code library**, not an **API endpoint**. D365 and Power Automate can't reference a .NET NuGet package directly — they need an **HTTP endpoint**.
- **The right pattern is a NuGet package + a hosted API**: The NuGet package contains the logic; a small hosted service (Azure Function or ASP.NET Core API) exposes it as an HTTP endpoint that D365/Power Automate can call.

**Recommendation:**
- **For the POC**: Use a simple console app or the existing portal code. Don't build a NuGet package yet — that's premature.
- **For the real solution**: Build the email logic as a **library** (could be a NuGet package) AND expose it as a **hosted API** (Azure Function or ASP.NET Core API). The library is for the portals; the API is for D365/Power Automate.

---

## 5. The "Multi-Dimensional Transaction" Challenge

The SA challenged:
> "how'd you use the recommended approach for a multi dimensional transaction"

### What this means
A "multi-dimensional transaction" here likely refers to:
1. **Multiple systems** (Portal, D365, Power Automate, Accreditation Portal) all sending emails
2. **Multiple template types** (assessment, accreditation, identity, marketing)
3. **Multiple branding** (APC, potential partner orgs — FR-007)
4. **Multiple channels** (transactional, marketing)
5. **Multiple states** (draft, review, approved, sent, delivered, opened, failed)

### How the recommended approach handles this

**The central email service handles multi-dimensionality via:**
- **Template categorisation** (FR-001): Templates tagged by system (assessment/accreditation), department, journey stage
- **Multi-branding** (FR-007): Template config stores branding per organisation (logo, colours, sender address)
- **API contract**: A single `POST /api/email/send` endpoint accepts `{ templateId, to, cc, bcc, data, branding }` — any system can call it
- **Activity tracking**: SendGrid event webhooks capture all send events regardless of source system
- **Audit trail**: All sends logged with source system, template, recipient, timestamp

**The NuGet package + hosted API pattern is exactly how you handle multi-dimensional transactions:**
- The **library** handles the "how" (template retrieval, rendering, sending)
- The **API** handles the "who" (which system is calling, auth, rate limiting)
- The **template store** handles the "what" (which template, which branding)
- The **event webhooks** handle the "when" (tracking across all systems)

---

## 6. Should We Also POC the Recommended (SendGrid) Approach?

**Yes — but only if time permits.** The POC's purpose is to compare Mailchimp vs SendGrid. If we only test Mailchimp, we have no comparison baseline.

**Minimal SendGrid POC (if time permits, ~1-2 hrs):**
1. Create a dummy template in SendGrid (Dynamic Template)
2. Retrieve it via API (`GET /v3/templates/{id}`)
3. Send a test email via the existing `ISendGridService`
4. Compare the API experience with Mailchimp

**However**, given the 1.5-day constraint, **prioritise the Mailchimp POC first**. The SendGrid comparison can be documented from existing knowledge (the portal already uses SendGrid, so we know its API works). The key unknown is **Mailchimp's template retrieval + transactional sending**, which is what the POC must prove.

---

## 7. Should We Abort the Previous Task (the Analysis)?

**No — don't abort the analysis.** The analysis document (`transactional-email-capability-analysis.md`) is still valuable as the **decision framework**. The POC is a **validation step** within that framework.

**However, the analysis should be updated to reflect:**
1. The **8-week timeline** (mid-late October 2026, not Dec 2027)
2. The **Monday.com project plan** (kick-off 28 July, tasks, success criteria)
3. The **POC findings** (once complete)
4. The **SA's NuGet package idea** and the **multi-dimensional transaction** consideration

**The POC and the analysis are complementary:**
- The **analysis** answers "which option is best?"
- The **POC** answers "can the chosen option actually work?"

---

## 8. What I Need From You

### To proceed with the POC, I need:

1. **Mailchimp trial account access** — or confirmation you'll create one
   - If you create it, share the API key (or add it to Key Vault as `MailchimpApi` — the portal already reads this)
   - Note: The portal's `MailchimpApi` secret in Key Vault is for the **production** Mailchimp account. For the POC, use a **separate trial account** to avoid touching production.

2. **Confirmation of the POC scope** — is it just the SA's 3 items (dummy template, API download, logging), or the BA's broader scope (sending, reporting, comparison)?

3. **Access to the Mailchimp.Net.V3 package** — it's already in the portal's csproj (`MailChimp.Net.V3` v5.8.2), so we can reuse it. No new package needed for the POC.

4. **A decision on where the POC code lives** — a standalone console app (cleanest for POC) or inside the portal (reuses existing DI/config)?

### What I'll deliver after the POC:
- A **POC results summary** (what worked, what didn't, API quirks)
- A **Mailchimp vs SendGrid comparison** (template mgmt, sending, reporting, logging, cost)
- A **recommendation** on whether to proceed with Mailchimp, SendGrid, or the central service approach
- An **updated analysis document** incorporating POC findings

---

## 9. POC Execution Plan (1.5 Days)

### Day 1 (Half Day)
| Time | Task |
|---|---|
| 0.5 hr | Create Mailchimp trial account, get API key |
| 0.5 hr | Create dummy template in Mailchimp |
| 1-2 hrs | Build minimal .NET console app: authenticate, list templates, get template HTML |
| 1 hr | Test sending a test email using the retrieved template |

### Day 2 (Full Day)
| Time | Task |
|---|---|
| 1 hr | Implement logging (console + file/SQL) |
| 1 hr | Test Mailchimp reporting/activity API |
| 1 hr | Document Mailchimp vs SendGrid comparison |
| 1-2 hrs | (Optional) Minimal SendGrid POC for comparison |
| 1 hr | Write POC results summary + recommendation |

---

## 10. Key Risks / Unknowns

| Risk | Impact | Mitigation |
|---|---|---|
| **Mailchimp trial doesn't include API access** | Can't test API | Check trial terms; may need paid plan or Mandrill trial |
| **Mailchimp transactional (Mandrill) is separate** | Sending test may need Mandrill account | Use SendGrid to send the retrieved template for comparison (tests template retrieval, not Mailchimp sending) |
| **Mailchimp API rate limits** | Slow testing | Test with small payloads |
| **Template HTML format** | Mailchimp templates use `*|MERGE|*` tags, not SendGrid's `{{var}}` | Document the difference; test merge field rendering |
| **1.5 days is tight** | May not complete all items | Prioritise: template retrieval + logging first (SA's scope), then sending + comparison if time permits |

---

## 11. Bottom Line

**The SA's POC suggestion is the right scope for 1.5 days.** It proves the core question: **can we retrieve a Mailchimp template via API and use it to send an email?** That's the foundation for everything else.

**The NuGet package idea is sound but premature for the POC.** Build the logic first, prove it works, then package it.

**The multi-dimensional transaction challenge is addressed by the central service pattern** (library + hosted API + template store + event tracking), not by a NuGet package alone.

**Don't abort the analysis** — update it with POC findings and the corrected timeline.

**The POC should focus on Mailchimp first** (the unknown), with SendGrid comparison documented from existing knowledge (the known).

---

*This plan is intended for the 1.5-day POC sprint. It should be validated with the technical team and adjusted based on Mailchimp trial account capabilities.*