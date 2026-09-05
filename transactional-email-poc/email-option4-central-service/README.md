# Track 2 — Central Email Service (Option 4)

The **Option 4 POC**: a thin Central Email Service proving one HTTP surface can serve every system (portals, Power Automate, D365-via-flow) with swappable providers.

Canonical documentation: [`../email-docs/`](../email-docs/) (doc 01 = design rationale, doc 02 = strategy/scorecard, doc 04 = full run guide).

## What this track demonstrates

| Concern | Implementation | Maps to |
|---|---|---|
| One shared endpoint | `POST /api/v1/email/send` with per-system keys | BRD FR-004 |
| Template governance | `templates.json` registry: stable key → provider template ID → owner → branding | FR-001 |
| Business-user editing | Templates edited in SendGrid's Design Editor; change is live without redeploying this service | FR-001 |
| Activity/reporting foundation | JSONL activity log + `GET /activity` + SendGrid event webhook receiver | FR-003 |
| Multi-branding | Branding configs per organisation resolved from template defaults | FR-007 |
| Vendor reversibility | `IEmailProvider` seam; `SendGridProvider` is one implementation | Option 4 core argument |
| SharedLib decoupling | Raw REST v3 call, no SharedLibrary reference | Migration story |

## Quickstart

```powershell
dotnet build EmailCentral.slnx
dotnet test                                    # 14 tests, no network needed

dotnet user-secrets init --project src/EmailCentral.Api.csproj
dotnet user-secrets set "SendGrid:ApiKey" "SG.xxxx" --project src/EmailCentral.Api.csproj
dotnet user-secrets set "ApiKeys:assessment-portal" "poc-key-assessment-001" --project src/EmailCentral.Api.csproj
dotnet user-secrets set "ApiKeys:powerautomate"     "poc-key-powerauto-002"  --project src/EmailCentral.Api.csproj
dotnet user-secrets set "ApiKeys:d365"              "poc-key-d365-003"       --project src/EmailCentral.Api.csproj

# Point templates.json at your real SendGrid Dynamic Template + verified sender, then:
dotnet run --project src/EmailCentral.Api.csproj    # http://localhost:5080
```

Works **without any keys** for: `/health`, OpenAPI at `/openapi/v1.json`, auth-rejection paths, webhook receiver, activity query. Only actual sends need the SendGrid key.

## Demo

Open `demo.http` in VS Code (REST Client extension) and run top-to-bottom — steps are numbered and each shows one decision-relevant behaviour. The money shot: edit the template in SendGrid's UI while the service runs, re-send, see the change live with zero deployment.

## Deliberate POC simplifications (record in FINDINGS)

- Registry is JSON, not SQL; no approval workflow UI (process-based governance instead)
- Preview render is indicative client-side substitution; authoritative rendering is SendGrid handlebars at send time
- Webhook endpoint is anonymous (SendGrid signature validation deferred)
- No queue/retry — callers get synchronous accept/fail; retry policy is production work
