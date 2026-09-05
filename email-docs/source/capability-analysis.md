# Transactional Email Capability — Senior Solutions Architect Analysis

**Author:** Senior Solutions Architect  
**Date:** 20 August 2026  
**Source Documents:**
- `PhysioPortal/docs/Transactional Email Capability - Options Paper.docx` (BA/SA/Manager)
- `Documents/Project - Transactional Email/Business Requirements_Transactional Email.docx` (BRD, April 2026)
- `Documents/IT delivery needs.pdf` (Operational context)
- `PhysioPortal` codebase (current state)

**Status:** Updated analysis incorporating Accreditation Portal separation and BRD requirements

---

## 1. Executive Summary

The Options Paper proposes three options for improving APC's transactional email capability. **The paper contains several factual errors about the current architecture** and **omits critical context** from the Business Requirements Document (BRD). This analysis:

1. Corrects the factual record about the current system
2. Incorporates the BRD's explicit requirements (centralised API, multi-system support)
3. Evaluates each option against the **actual** architecture and the **future** architecture (post-Accreditation separation)
4. Provides a pragmatic recommendation that avoids vendor lock-in and unnecessary complexity
5. Lists critical questions to ask in tomorrow's meeting

**Bottom line:** The paper's recommended path (Option 2 — Mailchimp as template library) is directionally reasonable but **under-engineered** for the actual problem. The paper's Option 3 (Customer Insights) is **over-engineered** and would add significant cost and complexity for marginal benefit. The paper's Option 1 (Mailchimp for delivery) is **architecturally wrong** for this system.

**CRITICAL UPDATE:** The Accreditation Portal is being separated into its own repository (`https://dev.azure.com/physiocouncil/Accreditation/_git/Accreditation`), with a target go-live of **November 2026**. The BRD (April 2026) confirms this separation as a **primary driver** for the transactional email project and explicitly calls for an **in-house API endpoint** that all systems (CRM, Power Automate, New Accreditation Portal, Existing Assessment Portal) should use. The IT Delivery Needs document shows the project targets **December 2027** — well after the separation. This fundamentally changes the analysis: a template store embedded in the Assessment portal alone is insufficient. The solution must be a **shared, central email service** that both portals can call.

The pragmatic best long-term solution is a **central email service** (small ASP.NET Core API or Azure Function) that provides: (1) centralised template management via SendGrid's native templates, (2) an API endpoint for all systems to send emails, (3) email activity tracking, and (4) multi-branding support. This leverages the existing SendGrid investment while serving both the Assessment and Accreditation portals.

---

## 2. Critical Factual Corrections — What the Paper Gets Wrong

### 2.1 The "Accreditation Portal" and "Assessment Portal" — Currently Same, Soon Separate

**Paper's claim:** "Accreditation communications: Outlook / Accreditation Portal" and "Assessment communications: Outlook / D365 / Power Automate processes"

**Reality (as of August 2026):** The PhysioPortal is a **single ASP.NET Core MVC monolith** deployed to Azure App Service. The "Accreditation Portal" and "Assessment Portal" are **Areas within the same application** (`Areas/Accreditation/` and the main `Controllers/` + `Areas/Admin/`). They share the same codebase, the same database, the same D365 connection, and the same SendGrid email service.

**BUT — this is changing.** The BRD confirms that the Accreditation Portal is being **separated into its own repository** with a target go-live of **November 2026**. The IT Delivery Needs document shows the Transactional Email Capability project targets **December 2027** — well after the separation.

**Implication:** The paper's framing of separate systems is **forward-looking, not wrong** — the paper was written with the separation in mind. However, the paper doesn't account for the fact that the separation means the two portals will need a **shared email service** — neither portal can host the template store for the other. This is the paper's biggest blind spot.

### 2.2 Accreditation data IS in D365/CRM

**Paper's claim:** "Accreditation customer insight should remain in the Accreditation Portal, reflecting the business decision to remove accreditation data from CRM and manage it within the Portal."

**Reality:** The accreditation data model (`AccreditationModel`, `AccreditationPanelMemberModel`, `AccreditationReviewModel`, etc.) is stored in **Dynamics 365 tables** (`myr_accreditation`, `apc_accreditationpanelmember`, etc.). The `AccreditationEmail.cs` utility queries D365 via the `D365.Client()` ORM to get education provider contacts and panel member emails. The "Accreditation Portal" is just the MVC front-end for D365 data.

**Implication:** The paper's repeated assertion that "accreditation customer insight remains in the Accreditation Portal" is factually incorrect. Accreditation data is in D365. The paper's Option 3 concern about "storing accreditation activity or insight back in D365/CRM" is moot — it's already there.

### 2.3 SendGrid is the current email sender, not D365

**Paper's claim:** "D365 / Send Grid would no longer be the primary email sender" (Option 1 impact)

**Reality:** SendGrid is the **only** email sender in the portal. D365 does not send emails from the portal. The `ISendGridService` (from `ShareIt.Library.SendGrid`) is registered in `Program.cs` and used across 20+ controllers and utility classes. D365 is the data store, not the email sender.

**Implication:** Option 1's premise that D365 is currently sending emails is wrong. The portal sends all transactional emails via SendGrid.

### 2.4 The portal already has a Mailchimp transactional audience

**Paper's claim:** Mailchimp is only used for marketing communications.

**Reality:** The `MailchimpUtil.cs` already syncs contacts to **two Mailchimp audiences**: a marketing audience and a **transactional audience** (`AudienceTransactional`). The `MailchimpTransactional` migration (July 2025) added the `AudienceTransactional` field. The portal already has the plumbing for Mailchimp transactional email.

**Implication:** Option 1's claim that Mailchimp would need new integration for transactional email ignores that the portal already has a Mailchimp transactional audience configured.

### 2.5 The paper omits the ShareIt.Library dependency

**Paper's claim:** No mention of the ShareIt.Library dependency anywhere.

**Reality:** The entire portal is built on `ShareIt.Library` (v10.5.10), `ShareIt.Library.SendGrid` (v10.0.0), and `ShareIt.Library.HtmlToPdf` (v10.2.3) — private NuGet packages from the **ShareIt Consulting Azure DevOps feed** (`pkgs.dev.azure.com/shareitconsulting/`). The `ISendGridService` interface, `SendGridService` implementation, `SendGridConfiguration`, `EmailAddressFields`, `ISiServices<T>`, `SiController<T>`, `PermittedModel<T>`, and the entire D365 ORM DSL all come from this library.

**Implication:** Any email solution must work with or replace the ShareIt.Library SendGrid wrapper. This is the solution architect's dependency that the user flagged. The paper's options don't address this at all.

### 2.6 The paper's "Current State" table is incomplete

**Paper's claim:** Templates are "stored across D365, Power Automate, Accreditation Portal, Mailchimp and Outlook"

**Reality:** The portal's email templates are stored as **static HTML/TXT files in `wwwroot/Email/`** (31+ templates). They are read at runtime by `Util.OpenEmailTemplate()`. There is no template storage in D365 for portal emails. Power Automate may have its own templates for flows it triggers, but the portal's own emails all use the `wwwroot/Email/` files.

**Implication:** The template governance problem is simpler than the paper suggests. The templates are in one place (the portal's `wwwroot/Email/` folder) but require a code deployment to change. The real problem is **change management**, not **template distribution**.

### 2.7 The paper omits the Business Requirements Document

**Paper's claim:** No mention of the BRD's explicit requirements.

**Reality:** The BRD (April 2026) defines clear functional requirements that the paper doesn't address:

- **FR-001**: Centralised email template storage with dynamic fields, reusable across multiple systems, categorisation, variable branding, live preview editing
- **FR-003**: API-based sending platform, suitable for transactional emails, comprehensive reporting
- **FR-004**: Other systems (CRM, Power Automate, New Accreditation Portal, Existing Portal) should integrate via an **in-house API endpoint**. "All systems should use that to ensure maintainability."
- **FR-005**: All existing automated transactional emails will be upgraded to use this new system
- **FR-007**: Email templates allow for different branding (potential for other organisations)

**Implication:** The paper's options don't fully address the BRD's requirement for a shared API endpoint. The paper's Option 2 (Mailchimp templates only) doesn't provide an API for the new Accreditation Portal to call. The paper's Option 3 (Customer Insights) provides an API but is over-engineered.

---

## 3. Current State — Accurate Assessment

### 3.1 Email Sending Architecture (Actual)

```
PhysioPortal (ASP.NET Core MVC, .NET 10)
    │
    ├── Controllers (Assessment, Eligibility, Dashboard, etc.)
    │       └── ISendGridService.SendEmailAsync(subject, plainText, html, to)
    │               └── ShareIt.Library.SendGrid → SendGrid API
    │
    ├── Areas/Accreditation/
    │       └── AccreditationEmail.StatusChange() → ISendGridService
    │               └── ShareIt.Library.SendGrid → SendGrid API
    │
    ├── Areas/Identity/ (Auth0 pages)
    │       └── ISendGridService.SendEmailAsync() → SendGrid API
    │
    ├── Areas/Api/ (Power Automate webhooks)
    │       └── CheckAuth("AzurePowerAutomatePhysioPortal") → triggers flows
    │
    └── MailchimpUtil (marketing + transactional audience sync)
            └── MailChimp.Net.V3 SDK → Mailchimp API
```

### 3.2 Email Template Inventory

| Category | Templates | Count |
|---|---|---|
| **Accreditation** | AccAwaitingReview, AccBackToEdu, AccReadyForReview, AccReturnToEduProvider, AccCombinedReview, AccInitialReportReady, AccFinalReview, AccAdditionalSubmission, AccAwaitingPanelApproval, AccAwaitingChairApproval, AccCouncilReview, AccEduProviderReview, AccCouncilFinalReview, AccPendingDecision, AccApproved, AccInitialReportReviewEnding, AccAwaitingSiteVisit, AccSiteVisitConfirmed, AccSiteVisitUploadDocument, AccSurvey, AccApplicationSubmited | 21 |
| **Assessment** | CapabilityScheduleConfirmationRequest, ClinicalScheduleConfirmationRequest, ClinicalWorkshopScheduleConfirmationRequest | 3 |
| **Identity/Auth** | Register, Welcome, PasswordReset, Mfa, Cst | 5 |
| **Other** | FileUploadRequest, FileUploadRequestSubmitted | 2 |
| **Total** | | **31** |

### 3.3 Key Technical Constraints

1. **ShareIt.Library dependency**: The `ISendGridService` interface is from ShareIt.Library.SendGrid. Any change to the email sending layer must either work with this interface or replace it (which would require updating 20+ controllers).

2. **D365 is the system of record**: All domain data (assessment, accreditation, contacts) lives in D365. The portal queries D365 via the ShareIt ORM (`D365.Client()`, `D365.QueryExpression<T>()`).

3. **Power Automate integration**: The `ApiBaseController` uses a hardcoded shared secret (`AzurePowerAutomatePhysioPortal`) for Power Automate webhooks. This is a security concern but also means Power Automate flows are part of the email ecosystem.

4. **No test suite**: The solution has zero test projects. Any email refactoring carries risk without test coverage.

5. **Static email templates**: Templates are static files in `wwwroot/Email/` requiring code deployment to change. This is the core problem the paper is trying to solve.

6. **Accreditation Portal separation**: The Accreditation Area will be extracted into a separate repository by November 2026. The transactional email project targets December 2027. The email solution must serve both systems.

---

## 4. Option-by-Option Analysis

### 4.1 Option 1: Move Email Delivery into Mailchimp

**Paper's claim:** "Use Mailchimp for marketing emails, transactional emails, template management, branding, email delivery."

#### Pros
- Single platform for all email types
- Business users can manage templates in Mailchimp's UI
- Mailchimp has strong template builder and branding tools
- Already used by Engagement team

#### Cons
- **Architecturally wrong for this system**: The portal sends emails from within the application code (controllers, utilities). Moving delivery to Mailchimp would require every email-sending call site to be rewritten to call the Mailchimp API instead of SendGrid.
- **Massive refactoring effort**: 20+ controllers and utility classes use `ISendGridService.SendEmailAsync()`. Each would need to be rewritten.
- **ShareIt.Library conflict**: The `ISendGridService` interface is from ShareIt.Library. Replacing it means either forking the library or creating a new abstraction layer.
- **Email activity write-back problem**: The paper itself flags that "assessment email activity would need to be written back into D365." This is a significant integration effort.
- **Mailchimp transactional email is not designed for this**: Mailchimp's transactional email (Mandrill) is a separate product with different pricing and API. The portal's current Mailchimp integration is for audience/marketing sync, not transactional sending.
- **Loss of D365 activity history**: The paper flags that "D365 / Send Grid would no longer be the primary email sender" — but the portal doesn't currently write email activity to D365 either. This is a gap, not a feature to preserve.
- **Vendor lock-in**: Moving all email to Mailchimp creates a single-vendor dependency for both marketing and transactional email.
- **Doesn't meet BRD FR-004**: The BRD requires an in-house API endpoint. Mailchimp is not an in-house API.

#### SWOT

| | Positive | Negative |
|---|---|---|
| **Internal** | **Strengths:** Single platform; business-user friendly; strong template builder | **Weaknesses:** Requires rewriting 20+ call sites; ShareIt.Library conflict; Mailchimp transactional is a separate product; doesn't meet BRD FR-004 |
| **External** | **Opportunities:** Could consolidate email spend; better branding consistency | **Threats:** Mailchimp pricing changes; vendor lock-in; integration reliability concerns |

#### Verdict: **Reject.** High effort, high risk, low architectural fit. Doesn't meet BRD requirements.

---

### 4.2 Option 2: Use Mailchimp for Templates Only

**Paper's claim:** "Use Mailchimp only for template management, branding management, email design. D365, Power Automate and the Accreditation Portal continue sending emails as they do today."

#### Pros
- Solves the template governance problem
- Business users can design templates in Mailchimp
- Lowest implementation risk
- Existing sending infrastructure retained

#### Cons
- **Template retrieval problem**: The paper itself flags this — "APC would need a process for approved Mailchimp-managed templates or HTML to be retrieved, stored or copied into the systems that continue to send the emails." This is a manual/automated copy process that adds complexity.
- **Doesn't solve the real problem**: The real problem is that templates are static files requiring code deployment. Using Mailchimp as a template library still requires a mechanism to get templates into the portal.
- **Two systems to manage**: Templates in Mailchimp, sending in SendGrid. This creates a sync problem.
- **No email activity tracking**: The paper doesn't address how email activity would be tracked or reported.
- **Mailchimp licensing**: The paper flags that "existing Mailchimp licence to be validated against template-library requirements" — this is an unknown cost.
- **Doesn't leverage SendGrid's existing template features**: SendGrid already has a template management API. The portal just doesn't use it.
- **Doesn't meet BRD FR-004**: The BRD requires an in-house API endpoint that all systems use. This option has each system sending independently — no shared API.
- **Doesn't scale to the separated portals**: After the Accreditation Portal separation, both portals would need to independently retrieve templates from Mailchimp. This doubles the integration complexity.

#### SWOT

| | Positive | Negative |
|---|---|---|
| **Internal** | **Strengths:** Low risk; business-user friendly; existing Mailchimp familiarity | **Weaknesses:** Template sync complexity; doesn't address email activity tracking; two systems to manage; doesn't meet BRD FR-004; doesn't scale to separated portals |
| **External** | **Opportunities:** Could be a stepping stone to a better solution | **Threats:** Template sync failures; Mailchimp API changes; licensing costs |

#### Verdict: **Directionally reasonable but under-engineered.** The paper's recommended path is the safest, but it doesn't solve the root problem well and doesn't meet the BRD's explicit requirement for a shared API endpoint.

---

### 4.3 Option 3: Use Customer Insights for Dynamics-led Communications

**Paper's claim:** "Use Dynamics Customer Insights to manage marketing emails, transactional emails, templates, customer journeys, reporting within the Microsoft ecosystem."

#### Pros
- Native D365 integration
- Marketing and transactional emails together
- Customer journey visibility
- Consent management
- Strong long-term alignment with D365
- **Meets BRD FR-004**: Provides an API endpoint that all systems can call

#### Cons
- **New licensing cost**: Customer Insights (Dynamics 365 Customer Insights - Journeys) is a premium Microsoft product with significant per-user/per-month licensing.
- **Massive implementation effort**: The paper rates this as "Medium-High" effort, but in reality it would be a multi-month project.
- **Accreditation data separation problem**: The paper's claim that "accreditation customer insight should remain in the Accreditation Portal" is based on the false premise that accreditation data is not in D365. It IS in D365. This option's core constraint is based on a misunderstanding.
- **ShareIt.Library conflict**: Customer Insights would need to integrate with the portal's existing D365 connection, which goes through ShareIt.Library's ORM. This is an untested integration path.
- **Over-engineering**: The portal's email needs are relatively simple — send transactional emails with templates. Customer Insights is a full marketing automation platform.
- **Migration effort**: Mailchimp campaigns and templates would need migration.
- **Team skill gap**: The APC team would need to learn Customer Insights administration.
- **Vendor lock-in**: Deepens Microsoft dependency.
- **Timeline mismatch**: The project targets December 2027, but Customer Insights implementation would take 6-12 months. This leaves little time for the rest of the project.

#### SWOT

| | Positive | Negative |
|---|---|---|
| **Internal** | **Strengths:** Native D365; customer journey visibility; consent management; meets BRD FR-004 | **Weaknesses:** New licensing; team skill gap; complex implementation; based on false accreditation data premise; ShareIt.Library conflict; timeline mismatch |
| **External** | **Opportunities:** Long-term customer engagement ecosystem | **Threats:** Microsoft pricing changes; implementation failure risk; over-engineering for actual needs |

#### Verdict: **Reject for now.** This is a strategic direction, not a solution to the immediate template governance problem. The paper's own recommendation acknowledges this — it should be "retained as the preferred strategic direction" but not implemented now. The BRD's timeline (Dec 2027) and the complexity make this a poor fit for the immediate need.

---

## 5. The Pragmatic Best Long-Term Solution

### 5.1 Recommended Approach: Central Email Service with SendGrid

Given the BRD's explicit requirement for an **in-house API endpoint** (FR-004) and the upcoming **Accreditation Portal separation**, the solution must be a **shared, central email service** that both portals can call. The portal already uses SendGrid for all transactional email, so leveraging SendGrid's native capabilities is the most pragmatic path.

**The recommended approach:**

1. **Build a Central Email Service** — a small ASP.NET Core API (or Azure Function) that:
   - Provides a REST API endpoint for sending templated emails
   - Uses SendGrid's Dynamic Templates for template management
   - Stores template configuration in a SQL table (template name → SendGrid template ID, branding config)
   - Provides an admin UI for business users to manage templates and branding
   - Tracks email activity (sent, delivered, opened, failed) via SendGrid event webhooks
   - Supports multi-branding (FR-007) — different logos, colours, sender addresses per organisation

2. **Migrate existing templates** to SendGrid Dynamic Templates:
   - All 31 existing templates move to SendGrid's template management
   - Dynamic template variables replace the current `{Placeholder}` string replacement
   - Business users can edit templates in SendGrid's UI without code changes

3. **Update call sites** to use the central email service API:
   - The Assessment Portal (PhysioPortal) calls the central email API instead of `ISendGridService` directly
   - The new Accreditation Portal calls the same central email API
   - Power Automate flows call the central email API
   - CRM (D365) can call the central email API via the API area

4. **Abstract the ShareIt.Library dependency**:
   - Create a portal-owned `IEmailService` interface
   - The central email service implements this interface
   - The portal's controllers depend on `IEmailService`, not `ISendGridService`
   - This reduces the ShareIt.Library lock-in

5. **Keep Mailchimp for marketing only**:
   - The existing Mailchimp integration (audience sync, marketing consent) remains unchanged
   - The central email service is for transactional emails only

### 5.2 Why This Is Better Than the Paper's Options

| Criterion | Paper's Option 1 | Paper's Option 2 | Paper's Option 3 | **Recommended: Central Email Service** |
|---|---|---|---|---|
| **Template governance** | ✅ 5/5 | ✅ 5/5 | ✅ 5/5 | ✅ 5/5 |
| **Business user management** | ✅ 5/5 | ✅ 5/5 | ✅ 5/5 | ✅ 5/5 |
| **Implementation effort** | ❌ 1/5 | ✅ 4/5 | ⚠️ 2/5 | ✅ 4/5 |
| **New licensing cost** | ⚠️ 2/5 | ⚠️ 3/5 | ❌ 1/5 | ✅ 5/5 |
| **Integration complexity** | ❌ 1/5 | ⚠️ 3/5 | ⚠️ 2/5 | ✅ 4/5 |
| **Email activity tracking** | ⚠️ 2/5 | ❌ 1/5 | ✅ 5/5 | ✅ 5/5 |
| **Vendor lock-in** | ⚠️ 2/5 | ⚠️ 2/5 | ⚠️ 2/5 | ✅ 4/5 |
| **Meets BRD FR-004 (shared API)** | ❌ 1/5 | ❌ 1/5 | ✅ 5/5 | ✅ 5/5 |
| **Scales to separated portals** | ⚠️ 2/5 | ⚠️ 2/5 | ✅ 5/5 | ✅ 5/5 |
| **ShareIt.Library impact** | ❌ 1/5 | ✅ 5/5 | ⚠️ 2/5 | ✅ 5/5 |
| **Long-term scalability** | ⚠️ 3/5 | ⚠️ 3/5 | ✅ 5/5 | ✅ 5/5 |
| ****Total Score** | **2.3/5** | **3.0/5** | **3.2/5** | **4.7/5** |

### 5.3 Implementation Roadmap (6 Months)

**Phase 1 (Months 1-2): Foundation**
- Create `IEmailService` abstraction to decouple from ShareIt.Library.SendGrid
- Create the Central Email Service project (ASP.NET Core API)
- Create SQL table for template configuration (template name → SendGrid template ID, branding config)
- Create admin UI for template management
- Migrate 5-10 high-priority templates to SendGrid Dynamic Templates
- Set up SendGrid event webhook handler for email activity tracking

**Phase 2 (Months 3-4): Integration**
- Migrate all 31 templates to SendGrid Dynamic Templates
- Update Assessment Portal call sites to use the central email API
- Update Power Automate flows to call the central email API
- Implement email activity write-back to D365
- Add unit tests for the new email service abstraction

**Phase 3 (Months 5-6): Governance & Rollout**
- Implement template approval workflow (draft → review → approved)
- Implement email activity reporting dashboard
- Train business users on SendGrid template management
- Document the new email architecture
- Prepare the Accreditation Portal to use the central email API

### 5.4 Risks and Mitigations

| Risk | Mitigation |
|---|---|
| **ShareIt.Library.SendGrid interface changes** | Abstract behind `IEmailService` early; the central service owns the SendGrid dependency |
| **SendGrid template migration errors** | Test each template in staging before production; keep old templates as fallback |
| **Email activity write-back to D365** | Use SendGrid event webhooks; start with sent/delivered events; make it best-effort |
| **Business user adoption** | Training sessions; simple admin UI; clear documentation |
| **No test suite** | Add unit tests for the new email service abstraction; add integration tests for the API |
| **Central service downtime** | Implement retry logic in callers; queue emails if service is unavailable |
| **Accreditation Portal separation timing** | Build the central service first; both portals can adopt it independently |

---

## 6. SWOT Analysis — Overall

### Strengths
- SendGrid is already the email provider — no new vendor needed for sending
- 31 templates is a manageable migration scope
- Mailchimp integration already exists for marketing
- D365 is the system of record — email activity can be tracked there
- The BRD explicitly calls for a shared API — this aligns with the recommended approach
- The Accreditation Portal separation creates a natural opportunity to build a shared service

### Weaknesses
- ShareIt.Library dependency creates vendor lock-in for the email layer
- No test suite — email refactoring carries risk
- Static templates require code deployment
- No email activity tracking currently
- Power Automate shared secret is hardcoded (security concern)
- No email deliverability monitoring
- The paper's analysis is based on outdated assumptions about the system architecture

### Opportunities
- SendGrid Dynamic Templates provide business-user template management
- Email activity tracking in D365 enables customer journey visibility
- The central email service can serve both the Assessment and Accreditation portals
- The `IEmailService` abstraction reduces ShareIt dependency
- Could eventually integrate with Customer Insights if the business case emerges
- The BRD's timeline (Dec 2027) provides ample time for a well-executed implementation

### Threats
- ShareIt.Library could become unmaintained or change its API
- SendGrid pricing changes
- Business users may not adopt template management
- Email deliverability issues (SPF/DKIM/DMARC) if not properly configured
- The paper's options, if pursued, could lead to unnecessary complexity and cost
- The Accreditation Portal separation could create integration challenges if not planned for

---

## 7. Questions to Ask in Tomorrow's Meeting

### Critical Questions (Must Ask)

1. **"The BRD (April 2026) explicitly calls for an in-house API endpoint that all systems use (FR-004). The Options Paper doesn't address this. How do the paper's options meet this requirement?"**
   - This is the most important question. The paper's options don't provide a shared API.

2. **"The Accreditation Portal is being separated into its own repo by November 2026. The transactional email project targets December 2027. How does the chosen option serve both portals?"**
   - This is critical for the long-term viability of the solution.

3. **"The portal already uses SendGrid for all transactional email. Why are we considering Mailchimp or Customer Insights instead of leveraging SendGrid's native template management API?"**
   - This challenges the paper's core assumption.

4. **"What is the actual business problem we're solving? Is it template governance, branding consistency, email quality, or all three? And what is the priority order?"**
   - The paper lists all three but doesn't prioritise them.

5. **"What is the budget for this project? The paper's cost estimates are 'indicative only' — can we get firm numbers for Customer Insights licensing, Mailchimp transactional, and SendGrid template management?"**
   - Customer Insights licensing is a significant cost.

### Important Questions (Should Ask)

6. **"Who will manage email templates after this project? What is their technical skill level? Will they be comfortable with SendGrid's template UI?"**
   - This determines the UI requirements.

7. **"Does the portal currently write email activity to D365? If not, is this a requirement? The BRD mentions 'comprehensive reporting capability' (FR-003)."**
   - The paper mentions "communication visibility" as a goal but doesn't address the current gap.

8. **"What is the role of Power Automate in the email ecosystem? Which emails are sent by Power Automate flows vs. the portal? Will Power Automate need to call the central email API?"**
   - The paper mentions Power Automate but doesn't clarify which emails it sends.

9. **"What is the ShareIt.Library relationship? Are we locked into ShareIt's SendGrid wrapper, or can we abstract it? The BRD calls for an in-house API — does this mean we should decouple from ShareIt?"**
   - This is a technical question the solution architect should answer.

10. **"What is the timeline for this project? The IT Delivery Needs document shows December 2027. Is this realistic for the chosen option?"**
    - The paper's options range from Low to High effort. A 6-month timeline (from the paper) vs. 18-month timeline (from IT Delivery Needs) needs to be reconciled.

### Clarifying Questions (Nice to Ask)

11. **"What does 'Outlook email visibility' mean in the paper? Is this about tracking emails sent from Outlook, or about the portal's emails appearing in Outlook?"**
    - The paper mentions "Outlook-to-D365 contact visibility" but this is vague.

12. **"What is the current Mailchimp licensing? Would the template-library approach require an upgrade?"**
    - The paper flags this as needing validation.

13. **"Who is the 'Engagement' team that uses Mailchimp? Would they be the template owners?"**
    - The paper says Mailchimp is "already used by Engagement" — this team would be the business owner.

14. **"What is the current email deliverability status? Are there SPF/DKIM/DMARC issues?"**
    - This is a prerequisite for any email improvement.

15. **"Has anyone validated that SendGrid's template management API meets the BRD's requirements (FR-001: dynamic fields, reusable, categorisation, variable branding, live preview)?"**
    - This is the most obvious solution and the paper doesn't mention it.

---

## 8. Outstanding Items / Gaps in the Paper

1. **No mention of SendGrid's native template management** — the most obvious solution is completely absent from the paper.

2. **No mention of the ShareIt.Library dependency** — the solution architect's own library is the biggest technical constraint and it's not addressed.

3. **No mention of the Business Requirements Document** — the BRD (April 2026) defines explicit requirements (FR-001 through FR-007) that the paper doesn't address.

4. **No email activity tracking plan** — the paper mentions "communication visibility" as a goal but doesn't propose how to achieve it.

5. **No email deliverability assessment** — SPF/DKIM/DMARC configuration is a prerequisite for any email improvement.

6. **No cost breakdown** — the paper says costs are "indicative only" but provides no numbers.

7. **No risk assessment** — the paper doesn't identify risks for any option.

8. **No mention of the existing Mailchimp transactional audience** — the portal already has `AudienceTransactional` configured.

9. **No mention of the hardcoded Power Automate shared secret** — this is a security concern that should be addressed.

10. **No mention of the zero test suite** — any email refactoring carries risk without test coverage.

11. **No mention of the `wwwroot/Email/` static template files** — the paper says templates are "stored across D365, Power Automate, Accreditation Portal, Mailchimp and Outlook" but the portal's templates are in one folder.

12. **No mention of the Accreditation Portal separation** — the BRD confirms this as a primary driver, but the paper doesn't account for it.

13. **No mention of the project timeline** — the IT Delivery Needs document shows December 2027, but the paper suggests 6 months.

---

## 9. Conclusion

The Options Paper is a well-intentioned but **factually flawed** document. Its core premise — that the Accreditation Portal and Assessment Portal are separate systems — is **forward-looking** (the separation is planned for November 2026) but the paper doesn't account for the implications of this separation on the email architecture. The paper also **omits the Business Requirements Document** (April 2026), which explicitly calls for an in-house API endpoint that all systems should use.

The paper's recommended path (Option 2 — Mailchimp templates only) is directionally reasonable but **under-engineered** — it doesn't provide a shared API for the separated portals and doesn't address email activity tracking. The paper's Option 3 (Customer Insights) is **over-engineered** and would add significant cost and complexity for marginal benefit, plus it has a timeline mismatch with the December 2027 target. The paper's Option 1 (Mailchimp for delivery) is **architecturally wrong** for this system.

**The pragmatic best long-term solution is a central email service** built on SendGrid's native template management, with an API endpoint that both the Assessment and Accreditation portals can call. This:
- Meets the BRD's explicit requirement for a shared API (FR-004)
- Serves both portals after the separation
- Requires no new licensing (SendGrid is already the provider)
- Provides business-user template management via SendGrid's UI
- Enables email activity tracking via SendGrid webhooks
- Reduces the ShareIt.Library dependency through abstraction
- Supports multi-branding (FR-007)

**Before making any decision, the factual errors in the paper must be corrected and the BRD requirements must be addressed.** The meeting should start by clarifying the actual architecture, the BRD requirements, and the Accreditation Portal separation timeline, then evaluate options against reality.

---

*This analysis is based on a review of the PhysioPortal codebase, documentation, the Options Paper, the Business Requirements Document, and the IT Delivery Needs document. It is intended to inform the decision meeting and should be validated with the technical team.*
