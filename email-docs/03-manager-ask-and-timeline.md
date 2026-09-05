# Manager Ask — Access, Time, and Meeting Talking Points

**Date:** 22 August 2026
**Purpose:** Everything to request from the manager (and BA/SA) before and during next week's meeting.

---

## 1. Access & Resources to Request

| # | Ask | From | Why | Urgency |
|---|---|---|---|---|
| 1 | **SendGrid scoped API key** (or subuser login) — scopes: Mail Send, Templates Read/Write, Sender Verification. Explicitly *not* the production key. | Manager / whoever admins the SendGrid account | Track B and Track C cannot run without it. A subuser keeps POC sends isolated from production reputation. | **Day 0** |
| 2 | Confirm **current SendGrid plan tier + monthly volume** | Same | Determines the honest "pricing" answer and which reporting features exist (Email Activity history is paid-tier). | Day 0 |
| 3 | **Mailchimp trial status** — confirm the Standard trial already created stays isolated from the production/Engagement account; do not point POC code at production keys | Manager / Engagement team | Keeps the POC clean and avoids touching live audiences. | Day 0 |
| 4 | **DNS records for Mandrill domain authentication** — Mandrill's demo tier (confirmed available) requires *Confirm domain* + *Authenticate domain* (SPF/DKIM) before ANY send, and only delivers to recipients at that authenticated domain. Ask IT to add the records for a POC subdomain (e.g. `email-poc.<apc-domain>`) or approve use of an existing test domain, and confirm the test recipient (your work address) is at that domain. No budget needed — the ~US$20 paid block is no longer required for the POC. | Manager / IT | Unblocks the real Mailchimp→Mandrill send-path test in Branch 1. Until DNS is done, Branch 1 still runs fully via the SendGrid path. | Day 0–1 |
| 5 | **Azure sandbox slot** for the demo (Function/App Service free tier in the dev subscription) — or agreement localhost demo is acceptable | Manager / IT | Track C demo hosting. Free tiers suffice. | Day 1 |
| 6 | Key Vault: whether to add a POC secret entry convention (e.g. `SendGrid-Poc`) or keep POC keys local-only via user-secrets | IT | Security hygiene; keys must never be committed. | Day 1 |
| 7 | **Accreditation repo access** (`dev.azure.com/physiocouncil/Accreditation/_git/Accreditation`) | Manager | Needed to design the integration story for the separated portal — both portals must be served by whichever option wins. | Before final decision |
| 8 | Named **business-user volunteer** (likely from Engagement team) to try editing a template in the SendGrid editor during the demo window | BA | Turns "business users can manage templates" from a claim into evidence. | Day 2 |

### 1a. Monday Checklist — Copy-Paste Messages

**To manager (single message covering items 1, 2, 4, 5, 7):**

> Subject: Access needed Mon/Tue — Transactional Email POC
>
> To complete the POC the team asked for, I need four things:
>
> 1. **SendGrid**: a scoped API key (or subuser login) on our tenant — scopes only: Mail Send, Templates Read/Write, Sender Verification. Not the production key. Also please confirm our plan tier + monthly volume (needed for the pricing comparison).
> 2. **Azure**: confirm which dev subscription/resource group I may use IF we later want a hosted demo URL. Nothing provisioned yet — the POC runs locally at zero cost; this is just pre-approval for a free-tier App Service/Function slot if wanted.
> 3. **DNS for Mailchimp transactional (Mandrill) POC**: their free demo tier requires domain confirmation + SPF/DKIM authentication before any test send, and only delivers to recipients at that authenticated domain. Request IT add the records for a POC subdomain (or approve an existing test domain). No spend required — the previously-mooted ~US$20 paid block is no longer needed for the POC.
> 4. **Access**: the Accreditation repo (already cloned — thank you) plus confirmation of who administers SendGrid/Mailchimp accounts.

**Mailchimp trial (self-serve today, item 3):** create a personal 14-day Standard trial account with a work email, do NOT reuse the production/Engagement account, generate an API key (Account → Extras → API keys), store it in user-secrets only. Flag to Engagement/manager that a throwaway trial exists purely for the POC so nobody mistakes it for the real tenant.

**SendGrid request wording** (if asked to formalise): see the ready-made message in the previous chat summary / reproduce item 1 above verbatim — the scope list matters (least privilege, subuser isolation from production reputation).

## 2. Time Ask

Formalise **3 working days** for the POC sprint (0.5 day setup + 2 days execution + 0.5 day demo prep), framed as:

> "To give you a defensible comparison rather than a rushed subset, I need three days and the four access items above. If the meeting can't move, I'll deliver the reduced scope in 1.5 days, but the Mailchimp sending comparison will necessarily be 'documented, not demonstrated' because of the Mandrill licensing gap."

This mirrors the draft email in `source/email-draft-to-ba.md` but with concrete asks attached.

**Success criteria to lock BEFORE starting (avoid moving goalposts):**
1. Same dummy template content across all tracks
2. Demonstrated: retrieve template → render merge data → send → log → read back status
3. Pricing sheet completed with validated numbers or explicit "vendor confirmation pending"
4. Scorecard completed for all three tracks
5. 15-minute recorded/walkthrough demo available before the meeting

## 3. Talking Points for the Meeting

Frame as **completing the options paper**, not overturning it:

1. **"The POC produced evidence for all three paper options plus a fourth."** The technical validation the paper itself calls for has started; here are results.
2. **"Mailchimp's transactional product is separate and paid."** Template retrieval works; actual transactional sending/reporting requires Mandrill provisioning (~US$20/block on top of Standard). That materially affects Options 1 and 2's cost rows.
3. **"SendGrid already does the thing we assumed only Mailchimp could."** Dynamic Templates give business users a no-code editor with previews and versioning — demonstrated live. The paper's Option list omitted the incumbent's capability entirely.
4. **"The BRD requires one in-house API endpoint (FR-004) that D365 and Power Automate can call. A NuGet package alone can't serve them; neither can per-system sending. Option 4 adds exactly that thin layer."** It meets FR-001/003/004/005/007; no other option does.
5. **"Option 4 makes the vendor decision reversible."** If APC later wants Customer Insights journeys or Mailchimp designs, they become adapters or design tools — not rewrites. The decision stops being a bet.
6. **Ask:** agree to progress Option 4 to the same technical validation the paper recommends for Options 2/3, with the Accreditation Portal separation (Nov 2026) as the forcing function.

If challenged on timeline/skills: the service is a few hundred lines on a stack the team already runs (.NET/Azure), with the cutover being a single DI registration change behind the existing `ISendGridService` interface — low-risk, incrementally adoptable, fully reversible at every step.

## 4. Open Questions Carried Forward (From source/capability-analysis.md §7)

Top five still unanswered and material to the decision:
1. Who owns template content long-term (Engagement? Accreditation team?) and who approves changes?
2. Is email activity write-back to D365 actually required for assessment communications, or is central activity logging sufficient? (Affects scope, not architecture.)
3. What does "Outlook email visibility linked to D365 contacts" concretely mean? (Kept out of POC scope.)
4. Current email deliverability posture (SPF/DKIM/DMARC verified sender status)?
5. Firm budget envelope — even indicative — for licensing deltas across options.

## 5. What Happens After Sign-off

1. Winning approach gets proper tests + integration documentation on its branch
2. Integration specs written per system (portal facade swap, Power Automate pattern, Accreditation Portal onboarding)
3. Revised options paper section drafted with POC evidence and scorecard
4. Only then: discussion about committing/pushing anything to a shared remote
