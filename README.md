# Email Architecture Comparison Demo

This is a separate, local-only demonstration of the two architectures being considered after Mandrill was selected:

1. **Shared library/distributed sending:** `Apc.Email.SharedLibraryDemo` references `Apc.Email.Mandrill` and calls Mandrill directly.
2. **Central service:** `Apc.Email.CentralApi` exposes one HTTP contract, a support view, an audit endpoint and a Mandrill adapter.
3. **Recommended hybrid:** `Apc.Email.Client` is a thin typed client for .NET applications; it calls the central API and contains no Mandrill code.

## Run locally

From this directory:

```powershell
dotnet build EmailArchitectureComparison.slnx
dotnet run --project src/Apc.Email.CentralApi
```

Open `demo.http` with VS Code REST Client. The API runs in simulation mode unless `MANDRILL_API_KEY` is set. This makes the demo safe to run without sending email.

Open `http://localhost:5080/` for the support view. Send two requests from `demo.http`, refresh the page, and search by recipient or source system.

For a real Mandrill send, set secrets only in the process environment:

```powershell
$env:MANDRILL_API_KEY = '<rotated-key>'
$env:FROM_EMAIL = 'info@physiocouncil.com.au'
$env:DEMO_TO_EMAIL = '<authenticated-recipient>'
dotnet run --project src/Apc.Email.SharedLibraryDemo
```

Mandrill must contain templates named `assessment-booked` and `welcome`, or change the request/template mapping for the test account. The demo defaults to simulation so the presentation does not depend on provider availability.

## 15-minute walkthrough

1. Run the API and open `/health`.
2. Send the nested Assessment Booked request from `demo.http`; explain that the API accepts candidate, assessment and session data without needing a new method for every field.
3. Send the second request with `accreditation-demo`; explain that the same API serves another application.
4. Open `/` and search the recipient. Show source system, template, status and correlation ID.
5. Run the unauthorised request and show `401`.
6. Run `Apc.Email.SharedLibraryDemo` and explain that it sends directly through the reusable library, with no central runtime dependency.
7. Show the two architecture diagrams and explain the hybrid recommendation.

## Azure deployment

Terraform is under `infra/`. It is intentionally not applied automatically. The existing personal subscription has an F1 App Service plan and storage in `rg-preetham-lab`; no APC subscription resources are referenced. The full production shape is documented in `docs/ARCHITECTURE-DEMO.md`.
