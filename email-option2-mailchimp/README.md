# Branch 1 — `poc/email-option2-mailchimp`

Validates the paper's **Option 2** (Mailchimp as central template library) — exactly what the BA/SA/manager asked the POC to prove. Local-only branch; never push.

Canonical documentation: `docs/technical/transactional-email/` on branch `poc/email-docs` (docs 02 strategy, 03 manager asks, 04 run guide).

## Quickstart

```powershell
dotnet restore
dotnet user-secrets init --project MailchimpPoc.csproj
dotnet user-secrets set "Mailchimp:ApiKey" "<key-datacenter>" --project MailchimpPoc.csproj
dotnet user-secrets set "SendGrid:ApiKey"  "<SG.xxxx>"        --project MailchimpPoc.csproj
dotnet user-secrets set "Poc:ToEmail"      "you@example.org"  --project MailchimpPoc.csproj

dotnet run --project MailchimpPoc.csproj -- selftest   # works without keys (offline proof)
dotnet run --project MailchimpPoc.csproj               # interactive menu
```

## What each menu option proves

| Option | Evidence produced |
|---|---|
| 1 List templates | Mailchimp API auth + template catalogue works from code |
| 2 Get template HTML | The core Option 2 assumption: approved HTML retrievable as a string for other systems |
| 3 Render sample | Merge-tag semantics (`*|TAG|*`) demonstrated client-side; authoritative rendering is Mandrill's job at send time |
| 4 Full pipeline | Retrieved Mailchimp template → merge render → **send via SendGrid** → structured JSONL log (the agreed fallback proving end-to-end usability) |

Every operation appends `logs/poc-log-yyyyMMdd.jsonl` — the logging comparison evidence.

## Before running

1. Create the Mailchimp **14-day Standard trial** (isolated from the production/Engagement account) and one dummy template (classic builder; include `*|FNAME|*`).
2. Generate an API key (Account → Extras → API keys) — format `KEY-dc`.
3. Mandrill (Mailchimp transactional sending) is **document-only** per decision: it needs a paid Standard plan + purchased blocks (~US$20/25k). Record the licensing facts in `FINDINGS.md` instead of testing them.

## Findings

Fill `FINDINGS.md` as you go — it feeds the scorecard/pricing sheet in doc 02 and the revised options paper.
