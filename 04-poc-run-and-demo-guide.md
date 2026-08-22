# POC Run & Demo Guide

**Purpose:** Every command you need to set up, run, test and demo both POC branches. Nothing here requires Azure provisioning or payment.

---

## 0. Prerequisites & Branch Map

- .NET SDK 10.x (installed: 10.0.303)
- VS Code with the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) for `demo.http`, or any HTTP client
- Accounts: Mailchimp **14-day Standard trial** (you create today), SendGrid **scoped key** (request Monday — see doc 03 §1a)

```
master                              ← clean, untouched
poc/email-docs                      ← these documents
poc/email-option2-mailchimp         ← Branch 1: Mailchimp retrieval harness (console app)
poc/email-option4-central-service   ← Branch 2: Central Email Service (API + tests)
```

All branches exist locally only. Never push them.

### What YOU must do manually (I cannot click vendor portals)

| Step | Where | Time |
|---|---|---|
| Create Mailchimp trial account + API key | mailchimp.com → Account → Extras → API keys | ~10 min |
| Create one dummy template in Mailchimp (classic builder if asked; add merge tag `*|FNAME|*`) | Mailchimp → Templates | ~10 min |
| Create one Dynamic Template in SendGrid with `{{firstName}}` handlebars + test data | SendGrid → Templates → Dynamic | ~10 min (after key arrives) |
| Verify a sender identity in SendGrid (Settings → Sender Authentication) — needed before any send | SendGrid UI | ~5 min |

---

## 1. Branch 1 — `poc/email-option2-mailchimp`

### Setup
```powershell
git checkout poc/email-option2-mailchimp
cd poc/email-option2-mailchimp
dotnet restore
dotnet user-secrets init --project MailchimpPoc.csproj
dotnet user-secrets set "Mailchimp:ApiKey" "xxxxxxxxxx-us21" --project MailchimpPoc.csproj   # from trial account
dotnet user-secrets set "Mandrill:ApiKey" "xxxxxxxxxxxxxx" --project MailchimpPoc.csproj     # Transactional > Settings > API keys (demo tier)
dotnet user-secrets set "SendGrid:ApiKey" "SG.xxxxxxxx" --project MailchimpPoc.csproj        # from scoped key (Monday)
dotnet user-secrets set "Poc:ToEmail" "you@apc.gov.au" --project MailchimpPoc.csproj
```

`Mailchimp:ApiKey` format is `KEY-DATACENTER` (e.g. `abc123-us21`) — the datacenter suffix drives the base URL automatically.

### Run / Test
```powershell
dotnet run --project MailchimpPoc.csproj                 # interactive menu
dotnet run --project MailchimpPoc.csproj -- selftest     # non-interactive: config check + offline render proof (works without keys)
```

Menu options:
1. List templates (Mailchimp API)
2. Get template HTML by ID → prints first 500 chars, saves full HTML to `logs/template-{id}.html`
3. Render sample data into merge tags (`*|FNAME|*` → `Jane`) — client-side preview only
4. **Full pipeline via SendGrid**: get template → render locally → send via SendGrid → log everything
5. **Full pipeline via Mandrill**: get template → send RAW HTML + `global_merge_vars` (`merge_language=mailchimp`) → **Mandrill renders server-side** → returns per-recipient status/reject_reason
6. Exit

Mandrill prerequisites (demo tier): Transactional → *Create API key*; *Confirm your domain*; *Authenticate your domain* (SPF/DKIM DNS — IT ask). Sends are rejected until authentication completes, and only deliver to recipients at that authenticated domain (gmail/external addresses will be rejected — capture the `reject_reason` as evidence). The selftest pings Mandrill (`PONG!`) when the key is set, so you can validate the key before DNS is done.

Every operation appends a JSONL line to `logs/poc-log-yyyyMMdd.jsonl`: `{ts, op, target, status, latencyMs, error}` — this is your "logging vs SendGrid" evidence.

### Findings
Fill `FINDINGS.md` as you go — gotchas checklist lives in there and mirrors doc 02 §6. Record exact API responses (redact keys) as evidence.

---

## 2. Branch 2 — `poc/email-option4-central-service`

### Setup (after SendGrid key arrives)
```powershell
git checkout poc/email-option4-central-service
cd poc/email-option4-central-service
dotnet restore
dotnet user-secrets init --project src/EmailCentral.Api.csproj
dotnet user-secrets set "SendGrid:ApiKey" "SG.xxxxxxxx" --project src/EmailCentral.Api.csproj
# Per-system caller keys (any values you invent; callers present them in X-Api-Key):
dotnet user-secrets set "ApiKeys:assessment-portal" "poc-key-assessment-001" --project src/EmailCentral.Api.csproj
dotnet user-secrets set "ApiKeys:powerautomate"     "poc-key-powerauto-002"  --project src/EmailCentral.Api.csproj
dotnet user-secrets set "ApiKeys:d365"              "poc-key-d365-003"       --project src/EmailCentral.Api.csproj
```

Then edit `src/templates.json`: replace the placeholder `providerTemplateId` (`d-REPLACE_ME`) with your real SendGrid Dynamic Template ID, and set `fromEmail` to your verified sender.

### Build / Test / Run
```powershell
dotnet build                                            # solution-wide
dotnet test                                             # unit tests (no network needed)
dotnet run --project src/EmailCentral.Api.csproj        # serves http://localhost:5080
```

Smoke check (runs without any keys): `GET http://localhost:5080/health` → `{"status":"ok"}`. Swagger/OpenAPI JSON at `/openapi/v1.json`.

### Demo walkthrough (`demo.http`)
Open `demo.http` in VS Code (REST Client) and execute top-to-bottom:

1. `GET /health` → live service, zero infrastructure
2. `POST /api/v1/email/send` (PasswordReset) → real email arrives; response includes `messageId`
3. Wrong/no `X-Api-Key` → 401 (shows per-system auth)
4. Unknown templateKey → 404 (shows registry governance)
5. `GET /api/v1/templates` → catalogue view (what business users own)
6. `GET /api/v1/templates/PasswordReset/preview` → indicative render
7. `GET /api/v1/activity?take=20` → audit trail

**Business-user money shot:** while the service runs, open SendGrid → your Dynamic Template → edit heading/colour → Publish version → re-run step 2 → email reflects the change with **zero redeployment**. Record this as a screen capture.

### Event webhook (optional, for delivery-status evidence)
Run a tunnel (`ngrok http 5080`) → add endpoint `https://<tunnel>/api/v1/events/sendgrid` in SendGrid → Event Webhook settings (enable Delivered/Bounced/Open) → events append to the activity log. Skip if time-boxed; note it in findings.

---

## 3. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Mailchimp 401 | Key missing datacenter suffix or wrong account | Re-copy `KEY-dc` format key |
| Mailchimp template GET returns odd markup | Newer builder template type | Note in FINDINGS (gotcha #1); create a classic-builder template instead |
| SendGrid 403 `sender identity not verified` | Unverified From address | Verify sender in UI; match `templates.json` branding |
| SendGrid 400 `template id not valid` | Placeholder ID not replaced | Edit `templates.json` |
| SendGrid 403 on send with correct key | Key lacks Mail Send scope | Regenerate key with scopes from doc 03 |
| Mandrill status `rejected`, reason `unsigned`/domain error | Domain not authenticated yet (demo tier rule) | Complete Confirm + Authenticate domain steps; verify recipient is at the authenticated domain |
| Mandrill rejects gmail/external recipient | Demo tier: recipients must be at authenticated domain | Expected behaviour — record reject_reason in FINDINGS as comparison evidence |
| Port 5080 busy | Another process | `dotnet run --project src -- --urls http://localhost:5090` |
| Tests fail on first run | Stale obj/ from branch switches | `git clean -xdn` to inspect, then `git clean -xdf` inside the poc folder only |

## 4. Evidence Capture (For the Meeting)

1. Screen recording of both demos (Branch 1 pipeline; Branch 2 money shot) — 5 min total
2. `FINDINGS.md` from each branch committed on its branch
3. Scorecard + pricing sheet filled in doc 02 on `poc/email-docs`
4. Pre-read email to BA/SA attaching doc 01 + scorecard the day before the meeting
