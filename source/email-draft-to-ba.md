# POC Catch-Up Email Draft

**Author:** Preetham  
**Date:** 21 August 2026

---

## Email to BA / Team

**Subject: Transactional Email POC - Scope Confirmation & Timeline**

Hi [BA name],

Thanks for setting up the catch-up. Before we meet, I want to clearly flag what is realistically possible given we have **signed up for Mailchimp (standard) only** at this stage.

**What I CAN test with Mailchimp-only:**
- Create a dummy template in Mailchimp
- Retrieve the template HTML via API (the core feasibility check)
- Log the API calls (success/failure)
- Send the retrieved template via SendGrid (which the portal already uses) to validate the end-to-end path

**What I CANNOT test without Mandrill:**
- **Transactional email sending from Mailchimp** - Mandrill is a *separate product* with a *separate account*. The standard Mailchimp licence does not include transactional sending.
- **Mailchimp's transactional reporting/logging** (delivery, opens, bounces per send) - this only exists in Mandrill.
- A true "compare with SendGrid" on sending/reporting is therefore **not possible until Mandrill is provisioned**.

**Proposed architecture (for discussion):**
A **NuGet package alone cannot serve D365 and Power Automate** - they cannot reference a .NET package; they need an **HTTP API endpoint**. The right pattern is a **shared library (NuGet) + a small hosted API** (Azure Function or ASP.NET Core API). The library serves the portals; the API serves D365/Power Automate.

**Proposed timeline:**
The current POC (template retrieval + logging) can be attempted in the available time. However, to do it properly - including the Mandrill comparison and the API + library pattern - I recommend **pushing the catch-up one week** so I can:
1. Get Mandrill provisioned
2. Complete a meaningful Mailchimp vs SendGrid comparison
3. Prepare a demo of the API + library pattern
4. Present findings rather than a rushed subset

Happy to align before the meeting.

Thanks,
Preetham

---

## Key Points to Land in the Meeting

1. **Mailchimp-only != transactional** - Mandrill is a separate product/account. This is the single most important caveat.
2. **What is testable now** - template retrieval + logging + send-via-SendGrid (the SA's scope, achievable).
3. **API + NuGet > NuGet alone** - clear technical reasoning: D365/PA need HTTP, not a .NET package.
4. **Push 1 week** - framed as "do it properly" rather than "can't do it," respecting their urgency while setting realistic expectations.