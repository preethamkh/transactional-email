# Implementation Plan — Production Build (Both Options)

This is the plan for the **real** feature, not the demo. Two build paths are covered:

- **Normal development** (you, as a .NET developer, following the architecture).
- **Agent-assisted development** (using agentic tools/LLM to build against a brief).

Both assume: Mandrill chosen as provider, a **central email service** as the recommended architecture, with the **shared-library** path documented for comparison and for teams that must be independent.

---

## Decision recap

| Approach | What it is | D365/PA? | Recommendation |
|---|---|---|---|
| Shared library | Each .NET app sends via a package | No — still needs HTTP | Valid but incomplete alone |
| Central service | One HTTP API, Mandrill behind it | Yes | **Recommended** |
| Hybrid | Central API + thin `.NET client` | Yes | Best DX for portals + one Mandrill integration |

---

## Phase 0 — Foundations (sprint ~1–2)

- [ ] Confirm Mandrill account/domain authentication and template naming convention.
- [ ] Decide template-key registry ownership (who owns keys → Mandrill slugs).
- [ ] Confirm retention policy (hot 90 days, archive N years) and PII rules for email bodies.
- [ ] Decide environment strategy: separate email/audit DB (recommended) vs. shared schema.
- [ ] Set up Terraform state backend and CI/CD pipeline skeleton.

**Exit:** agreed contracts + naming + ownership + environment boundaries.

---

## Phase 1 — Core service (normal: ~2–3 wks; agentic: ~1–2 wks)

**Contracts**
- `EmailRequest`, `EmailSendResult`, `EmailAuditRecord`, `EmailEvent`.
- Versioned endpoint (`/api/v1/...`).
- Request validation (unknown keys, type checks, required fields).

**Provider seam**
- `IEmailProvider` with `SendAsync`.
- `MandrillProvider` implementation (from the demo adapter).
- Timeouts, cancellation, safe error mapping.

**API**
- Auth (per-system keys → later Entra/managed identity).
- Idempotency enforcement on `idempotencyKey`.
- Correlation IDs across send → webhook → audit → D365.
- Audit persistence to SQL.

**Tests (production-required, not demo)**
- Unit: contract validation, provider payload shape, auth, idempotency, event mapping.
- Integration: fake provider + ephemeral DB; happy path, timeout, rejected recipient, duplicate idempotency key.

**Exit:** service can send via Mandrill, record audit, enforce auth, and is covered by tests.

---

## Phase 2 — Reliability & events (normal: ~2–3 wks; agentic: ~1–2 wks)

- [ ] Service Bus topic `email-events` + subscriptions (audit-archive, d365-writeback, alerting).
- [ ] Mandrill webhook receiver: validate signature, enqueue events.
- [ ] Azure Function consumers:
  - audit archive → Blob (lifecycle policy)
  - alerting → Teams/email on bounce/failure thresholds
  - D365 write-back (later, separate owner)
- [ ] Retry policy + dead-letter queue.
- [ ] Observability: App Insights traces, metrics, alerts.

**Exit:** async event flow works, poison messages handled, monitoring in place.

---

## Phase 3 — Support UI (normal: ~1–2 wks; agentic: ~1 wk)

- [ ] Support search UI (by recipient, template, status, date, correlation ID).
- [ ] Entra ID login + RBAC (support vs compliance vs ops).
- [ ] Detail view (status history, metadata, optional body with strict access).
- [ ] Export (CSV/PDF) for disputes/audits.

**Exit:** support can answer "what happened to this email?" independently of provider retention.

---

## Phase 4 — Integration with calling systems (normal: ~2–3 wks; agentic: ~1–2 wks)

### PhysioPortal (existing .NET)
- Implement the thin `Apc.Email.Client` (calls central API) OR `HttpClient` directly.
- Facade/DI swap so existing controllers migrate with minimal change.
- Migrate templates to Mandrill keys.

### Accreditation Portal (new, Nov 2026)
- Reference `Apc.Email.Client` from day one; same contract.

### Power Automate
- HTTP action per flow → `POST /api/v1/email/send` with headers.
- Store `X-Api-Key` as protected flow configuration.
- Capture correlation ID for support.

### D365 (separate owner)
- Document only here — see `docs/ARCHITECTURE-DEMO.md` §D365. No direct changes by you.
- Custom connector from OpenAPI, or HTTP action in a flow.
- Decide D365 Email activity as system of record; async write-back via webhook consumer.

**Exit:** all systems can send through the same contract.

---

## Phase 5 — Governance & hardening (normal: ~2 wks; agentic: ~1 wk)

- [ ] Key Vault / managed identity for secrets (no keys in code/config).
- [ ] Terraform per environment (dev/test/prod), PR-based changes.
- [ ] Template approval workflow (draft → review → publish).
- [ ] Security review: auth, PII, logging retention, DLP.
- [ ] Load/performance smoke at expected volume.

**Exit:** production-ready, operable by a small team.

---

## Timeline summary (single .NET developer)

| Phase | Normal | Agentic-assisted |
|---|---|---|
| 0 Foundations | 1–2 wks | 1 wk |
| 1 Core service | 2–3 wks | 1–2 wks |
| 2 Reliability/events | 2–3 wks | 1–2 wks |
| 3 Support UI | 1–2 wks | 1 wk |
| 4 Integration | 2–3 wks | 1–2 wks |
| 5 Governance/hardening | 2 wks | 1 wk |
| **Total** | **~10–15 wks** | **~6–9 wks** |

> Agentic estimates assume an experienced engineer reviews and tests the output; agents accelerate generation, not verification. Integration with D365/Power Automate remains with their owners regardless of path.

---

## Agent-assisted build guidance

Use `AGENT-BUILD-BRIEF.md` as the instruction file. Requirements for agent handoff:

1. Inspect the repository first; never assume structure.
2. Change only the assigned folders/projects.
3. Run `dotnet build` + `dotnet test` after each change.
4. Never embed credentials or PII.
5. Never modify D365/Power Automate or the APC subscription.
6. Report assumptions, unresolved integration work, and any required human decisions.

---

## Testing strategy (production)

- **Unit:** contracts, auth, idempotency, provider payload, event mapping.
- **Integration:** fake provider + ephemeral DB; send/timeout/reject/duplicate-key paths.
- **Contract tests:** API consumers (portal, PA, D365) against a shared schema.
- **Smoke:** non-production Mandrill template + authenticated recipient.
- **Failure drills:** provider down, queue backlog, DLQ, idempotency replay.

The demo deliberately omits these; the production build must include them.
