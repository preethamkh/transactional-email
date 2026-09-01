# 15-Minute Meeting Demo Script

**Audience:** BA and a partially-technical team. Not a deep-dive into Azure.
**Goal:** Help the room decide between a **shared library** and a **central email service**, and show how Mandrill fits, how audit/logging is owned long-term, and how the nested data the architect questioned is handled.
**Format:** Local-only, no Azure provisioning required, no API key required (simulation mode). Safe and repeatable.

> Timings assume ~1 min per step. Skip freely; the three "must-show" steps are marked **[MUST]**.

---

## Setup (before the meeting, ~3 min once)

```powershell
cd poc/email-architecture-comparison
dotnet build EmailArchitectureComparison.slnx
dotnet run --project src/Apc.Email.CentralApi --urls http://localhost:5080
```

- Leave the terminal running.
- Open `demo.http` in VS Code with the **REST Client** extension installed.
- Open `http://localhost:5080/` in a browser (this is the support/audit UI).

---

## Demo (15 minutes)

### 1. Open `GET /health` — the service is live (30 sec) **[MUST]**
- In `demo.http`, click "Send Request" on the first block.
- Result: `{"status":"ok","provider":"mandrill","mode":"simulation"}`.
- **Say:** "This is one HTTP endpoint every system can call. Right now it runs locally with no email provider key, so nothing is actually sent — this is a safe demo."

### 2. Send a realistic email request (2 min) **[MUST]**
- Run the second `POST /api/v1/email/send` block.
- **Say:** "This is the payload a Power Automate flow, D365, or the portal would send. Notice it carries **nested data** — a candidate object, an assessment object, and a session object with its own location. The service handles multi-dimensional data without needing a new method for every field. This directly answers the '2D/3D data' concern."

### 3. Send from a second system (1 min)
- Run the third block (`sourceSystem: accreditation-demo`).
- **Say:** "The exact same contract works for a different system. No new code for the second caller."

### 4. Show the audit/support view (2 min) **[MUST]**
- Switch to the browser tab `http://localhost:5080/`.
- Search by recipient or status.
- **Say:** "The provider only keeps email detail ~30 days. Here we keep our own long-lived audit record: time, source system, template, recipient, status, and correlation ID. This is how support answers 'what happened to my email six months ago?' This is the logging-ownership story for the meeting."

### 5. Show the audit via API (1 min)
- Run the `GET /api/v1/activity` block in `demo.http`.
- **Say:** "The same audit the UI shows is available as a queryable endpoint, so a future support portal or dashboard can use it too."

### 6. Browse templates (1 min)
- Run `GET /api/v1/templates`.
- **Say:** "Templates are registered by a stable key that maps to a Mandrill template slug. Business users edit in Mandrill; callers don't care about the slug — they just use the key."

### 7. Prove auth (1 min)
- Run the unauthorized `POST` block (no `X-Api-Key`).
- Result: `401`.
- **Say:** "Each system gets its own API key, so we can control and revoke access per caller."

### 8. Show the shared-library alternative (2 min) **[MUST]**
- Stop the API (Ctrl+C) OR open a second terminal and run:
  ```powershell
  cd poc/email-architecture-comparison
  dotnet run --project src/Apc.Email.SharedLibraryDemo
  ```
  (It will print instructions unless `MANDRILL_API_KEY` and `DEMO_TO_EMAIL` are set.)
- **Say:** "This is the architect's alternative: each .NET app bundles a shared library that calls Mandrill directly. It works for .NET apps — but D365 and Power Automate cannot load a .NET library, so that path still needs an HTTP endpoint. That's the key point: with a library you often still need an API for D365/PA, so you end up maintaining both."

### 9. Show the architecture comparison (2 min)
- Open `docs/ARCHITECTURE-DEMO.md` and show the two mermaid diagrams.
- **Say:** "Two viable shapes. The recommendation is a hybrid: the central HTTP API for everything, with an optional thin .NET client for the portals so developers get a typed helper, but the Mandrill logic lives in one place."

### 10. Close with the recommendation (1 min)
- **Say:** "My recommendation: a thin central email service with an optional client package. It meets the BRD requirement for one in-house API, gives D365/Power Automate a path, keeps one audit trail, and keeps the vendor decision reversible. The library-only approach is valid for .NET but doesn't serve D365/Power Automate by itself."

---

## What to avoid in the room

- Do **not** deep-dive into Azure services unless asked.
- Do **not** show Terraform, Functions source, or `Program.cs` — those are engineering artifacts, not the decision.
- Do **not** claim anything was sent if it was in simulation mode; be explicit that it's a safe demo.

---

## Optional: real send (only if you want proof of an actual email)

Set these as environment variables before running (never commit them):

```powershell
$env:MANDRILL_API_KEY = "<rotated-key>"
$env:FROM_EMAIL = 'info@physiocouncil.com.au'
$env:DEMO_TO_EMAIL = '<an-authenticated-recipient>'
dotnet run --project src/Apc.Email.CentralApi --urls http://localhost:5080
```

Then re-run the send blocks. Requires Mandrill templates named `assessment-booked` and `welcome` on the test account.

---

## Optional: hosted demo (free-tier, personal subscription only)

If you want a public URL, see `infra/README.md` and `docs/COST-ANALYSIS.md`. This touches **only** the personal `bonny_kh@hotmail.com` subscription and never the APC subscription. Not required for the decision.
