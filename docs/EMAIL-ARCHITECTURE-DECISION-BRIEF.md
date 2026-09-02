# Transactional Email Architecture Decision Brief

## Executive decision

Recommend the **hybrid shape**: a central HTTP email service owns Mandrill, queueing, retries, audit and support visibility; a small typed client library makes calls easy for .NET applications. D365 and Power Automate call the central API through HTTP or a custom connector. APIM is optional and should be added only when gateway governance, quotas or external consumers justify it.

The central API is a microservice when it is independently deployed and owns this capability. The API is its interface; an endpoint is one route. App Service hosts the API. APIM is a gateway in front of it, not the API itself.

## Technology by option

| Concern | Option 1: Shared library | Option 2: Central service | Recommended hybrid |
|---|---|---|---|
| Caller | Each .NET application | .NET, D365 or Power Automate | .NET uses typed client; D365/PA use HTTP |
| Email code | NuGet/shared `Apc.Email.Mandrill` in each app | Central API owns Mandrill adapter | Central API owns Mandrill adapter |
| Public interface | Library method; HTTP still needed for D365/PA | HTTP API endpoint | Thin client calls HTTP API |
| Hosting | Inside each application | Azure App Service | Azure App Service |
| APIM | Not required | Optional gateway in front of API | Optional gateway in front of API |
| Queue | Optional per application | Azure Service Bus behind the API | Azure Service Bus behind the API |
| Worker | Optional background worker | Azure Function consumes Service Bus | Azure Function consumes Service Bus |
| Audit | Each app or shared audit sink | Separate Azure SQL audit DB | Separate Azure SQL audit DB |
| Support UI | Distributed or not included | Central UI/API | Central UI/API |
| Provider credentials | Repeated across applications | One central secret | One central secret |

## Request flow

`D365/PA/.NET caller -> optional APIM -> Central Email API (App Service) -> Service Bus queue -> Azure Function -> Mandrill`

The API should validate the request and publish a message. The Function performs the provider send, handles retry/dead-letter behaviour and writes the result to SQL. For the learning POC, a direct provider send followed by an audit message is also acceptable, but production should make the queue the durable boundary.

## Template and field mapping

Every option needs configuration somewhere; SQL is not inherently required. Use a canonical contract (`candidateName`, `assessmentDate`, `practitionerName`, `bookingId`) and make Mandrill variables follow that contract. D365/PA maps Dataverse fields to the contract, not to provider-specific names. A new template normally requires a stable key, Mandrill slug and required-field declaration in version-controlled configuration. Editing existing template content does not require deployment. A SQL registry/admin screen is later scope only, for runtime administration, approval, versioning or multiple providers.

## Component Q&A

**Is APIM the API?** No. APIM is an optional managed gateway that can expose a stable URL, enforce auth, quotas, rate limits, transformations, versions and documentation. App Service hosts the actual Central API.

**Is Service Bus exposed to callers?** No. It is behind the API. Callers submit one HTTP request and do not need Azure Service Bus credentials.

**What is `email-events`?** It is a queue inside the `emailarchlabnamespace` Service Bus namespace. It buffers work between the API and Function so temporary failures can retry without blocking the caller. It is visible in Azure Portal under the namespace's Queues.

**What does SQL store?** Durable searchable audit metadata: source, recipient, template key, correlation ID, provider ID, status, timestamps and errors. It is separate from the APC database and need not store full email bodies.

**Why not use the shared library everywhere?** It is simpler initially, but duplicates provider credentials, mapping, retry behaviour, upgrades and audit ownership across applications. The central service pays operational complexity in exchange for one integration boundary and consistent support.

**Can the API send real email?** Yes, when the Mandrill key and an authenticated test recipient are configured. Local simulation remains available by omitting the key and Service Bus settings. The personal lab is isolated in `rg-email-architecture-lab`; no APC resources are used.

## Cost and scale

At 7,000–8,000 emails per month, message volume is modest. Service Bus and Functions consumption are normally negligible at this volume. The main fixed costs are App Service, SQL and monitoring. The learning lab currently uses a Basic App Service plan because the F1 quota was exhausted, plus Basic SQL; production tier and retention determine the final bill. A reasonable production order-of-magnitude estimate is **tens to low hundreds of AUD/month**, excluding Mandrill, depending mainly on App Service/SQL tier, backup retention and monitoring. Confirm exact figures in Azure Pricing Calculator before approval.

The APC production subscription was not inspected because it is outside the approved personal-lab boundary. Its existing App Service, SQL, monitoring and integration resources must be reviewed by an authorised APC owner before claiming savings or reuse.

## Timeline and recommendation

| Stage | Indicative time |
|---|---:|
| Contracts, template convention and API | 1–2 weeks |
| Queue, Function, SQL audit and retries | 1–2 weeks |
| D365/PA integration and security testing | 1–2 weeks |
| Pilot, monitoring, documentation and rollout | 1–2 weeks |

Recommend a small pilot with two or three templates, prove the end-to-end flow, then onboard the remaining templates. Do not begin with APIM, SQL administration UI or multi-provider support unless a confirmed requirement justifies them.

## Handling compressed estimates

When a task is called “a few minutes” or “one day,” respond with a written scope and acceptance criteria rather than agreeing to an unsafe shortcut: “I can deliver a minimal spike in one day. Production-ready delivery also requires tests, failure handling, deployment, rollback and verification. Please confirm which scope and deadline is expected.” This keeps the discussion factual without challenging the person, and creates evidence when a band-aid implementation causes breakage.
