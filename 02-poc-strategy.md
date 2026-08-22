# POC Strategy — Two Branches, One Comparison

**Status:** Plan — supersedes the earlier three-track draft; replaces `source/poc-plan-draft.md`
**Date:** 22 August 2026 (rev 2)

---

## 1. Decision: Why Only Two POC Branches

The BA asked for: *"test sending emails, reporting, logging (compare with SendGrid), and getting templates out of MailChimp for D365 and Portal. Pricing."*

That request maps to **Branch 1** below. A senior-architecture review concluded that POCing every option wastes scarce time, because most options share unknowns or cannot be meaningfully spiked:

| Paper option | POC? | Rationale |
|---|---|---|
| Option 1 — Mailchimp delivery | **No** | Differentiator is Mandrill delivery, which requires a paid block (document-only per agreement). Architectural rejection (20+ call-site rewrites, write-back integrations) stands regardless of what a POC shows. |
| Option 2 — Mailchimp templates | **Yes → Branch 1** | Exactly what BA/SA/manager asked for. Proves template retrieval + logging; send falls back via SendGrid. |
| Option 3 — Customer Insights | **No** | Cannot be validated meaningfully without licensing decisions. Validation = vendor engagement + costings, not code. Revisit later as a possible adapter. |
| Option 4 — Central Email Service | **Yes → Branch 2** | The recommended option. Its SendGrid provider work inherently demonstrates SendGrid Dynamic Templates — producing the incumbent-baseline evidence "for free." |

**Net result:** two local branches produce evidence covering the BA's requirement, the best-of-paper option, the recommended new option, and the SendGrid comparison — with zero spend.

## 2. The Two Branches

| Branch (local only) | Validates | Maps to | Cost |
|---|---|---|---|
| `poc/email-option2-mailchimp` | Mailchimp template storage/retrieval via API; merge-tag semantics; logging; send-via-SendGrid fallback; pricing/gotcha findings | Option 2 (+ Option 1's sending question, documented) | $0 (trial account) |
| `poc/email-option4-central-service` | Thin central API: one endpoint, template registry, per-system keys, activity log, webhook receiver, Swagger/OpenAPI demo; SendGrid Dynamic Templates end-to-end | Option 4 + incumbent (SendGrid) baseline | $0 (existing account, free tier limits fine) |

Both share one conceptual harness: *list templates → get template → render with data → send → log → read status*. Branch 2 hosts the same operations behind HTTP.

## 3. Branch 1 — Mailchimp (Option 2) Scope

1. Dummy template created in Mailchimp UI (14-day Standard trial account, isolated from production).
2. Harness ops against `GET /3.0/templates`, `GET /3.0/templates/{id}` (returns `html` string).
3. Client-side merge-tag substitution demo (`*|FNAME|*`) — documenting that authoritative rendering happens in Mandrill at send time.
4. Send retrieved HTML via SendGrid REST (agreed fallback) — proves end-to-end content usability.
5. Logging: structured JSONL (timestamp, op, target, status, latency, error).
6. Findings: pricing sheet + gotchas checklist (§7) + explicit "Mandrill send/reporting not testable without paid block" record.

**Exit criteria:** retrieval demonstrated or definitively blocked (with cause); pipeline demoed end-to-end; findings complete.

## 4. Branch 2 — Central Email Service (Option 4) Scope

Build (~1 day, mostly assembly):
1. ASP.NET Core Minimal API (net10): `POST /api/v1/email/send`, `GET /api/v1/templates`, `GET /api/v1/templates/{key}/preview`, `GET /api/v1/activity`, `POST /api/v1/events/sendgrid`, `GET /health`.
2. `IEmailProvider` + `SendGridProvider` (raw REST v3, deliberately not ShareIt.Library — demonstrates decoupling; this doubles as the SendGrid evidence track).
3. Template registry (`templates.json`: key → provider template ID → branding → owner); naive `{{handlebars}}` preview substitution (authoritative rendering stays in SendGrid).
4. Per-system API-key auth (`X-Api-Key`), constant-time comparison.
5. Activity log (JSONL) + webhook receiver stub updating status.
6. Small xUnit suite (registry, preview, auth, payload builder) — starts the testing habit the monolith lacks.
7. `demo.http` walkthrough + README run instructions.

### Demo script (15 min, the showcase)
1. `GET /health` → service live locally, zero Azure provisioning
2. `POST /send` PasswordReset → email arrives
3. Business-user moment: edit template in SendGrid editor → save version → re-send → **live change, zero deployment**
4. Same endpoint, different `X-Source-System` key → D365/Power Automate path works identically
5. `GET /activity` → audit trail (FR-003 evidence)
6. Scorecard + pricing slide → incremental cost ≈ $0 vs Options 1–3

## 5. Comparison Framework (Fill During POCs)

Score 1–5 per criterion; weights sum to 100. Options 1/3 rows completed from documents, not code.

| Criterion | Weight | Opt 1 Mailchimp delivery | Opt 2 Mailchimp templates | Opt 3 Cust. Insights | Opt 4 Central svc (SendGrid now) |
|---|---|---|---|---|---|
| Meets FR-004 (shared in-house API) | 15 | | | | |
| No-code business-user editing | 12 | | | | |
| Centralised activity/reporting (FR-003) | 12 | | | | |
| Incremental licensing cost | 10 | | | | |
| Implementation effort/risk vs existing portal | 10 | | | | |
| Serves both portals post-separation | 8 | | | | |
| Multi-branding (FR-007) | 6 | | | | |
| ShareIt.Library impact | 6 | | | | |
| Vendor reversibility / lock-in | 6 | | | | |
| Operational burden | 5 | | | | |
| **Weighted total** | 100 | | | | |

### Pricing capture sheet (validate during POC; indicative until vendor-confirmed)

| Item | Mailchimp/Mandrill | SendGrid | Customer Insights |
|---|---|---|---|
| Platform licence/mo | Standard trial now; paid $ after | Existing licence; verify tier + volume | Not quoted |
| Transactional sending | Block ≈ US$20 / 25k emails / mo (verify terms) | Included in existing volume? verify tier | In CI licence |
| Reporting/history retention | Verify Mandrill retention | Verify paid-tier Activity history | Native |
| Integration build effort | High (write-backs) | Low | Medium-high |
| **POC outlay required** | $0 (document-only send) | $0 | n/a (not POC'd) |

## 6. Gotchas Checklist (Verify Each, Record Evidence in FINDINGS.md)

- [ ] Mailchimp template builder type affects `GET /templates/{id}` output (classic vs newer builders return different markup quality outside Mailchimp)
- [ ] Mailchimp marketing templates ≠ Mandrill templates — two stores under one brand (split-brain problem, evidenced)
- [ ] Merge tags render at **send time** in Mandrill, not retrieval
- [ ] Mandrill requires paid Standard + purchased blocks; confirm any trial/test allowance
- [ ] SendGrid free/current tier limits sufficient for POC; confirm Dynamic Templates + event webhook availability
- [ ] SendGrid Email Activity search/history tier requirements — document what's visible instead
- [ ] Domain authentication (SPF/DKIM/DMARC) status on the SendGrid account
- [ ] Current SendGrid plan tier + monthly volume (materially changes the pricing answer)

## 7. Git Rules (All Local)

```
master                              ← untouched; nothing committed here
├── poc/email-docs                  ← documentation commits (this folder)
├── poc/email-option2-mailchimp     ← Branch 1 harness + findings
└── poc/email-option4-central-service ← Branch 2 service + tests + demo
```

- Created locally from master; **never pushed**, no PRs
- Master working tree stays clean: docs live on `poc/email-docs`; switch with `git checkout poc/email-docs`
- Secrets only via `dotnet user-secrets` / environment variables; `.gitignore` excludes logs and local settings; keys never appear in committed files
- Winning branch gets fuller tests + integration docs before any remote discussion

## 8. Time Budget

| Day | Focus |
|---|---|
| 0 (half, today) | Docs committed; both branches scaffolded and building; access requests drafted |
| 1 | Branch 1 complete incl. findings (needs Mailchimp trial key); SendGrid request lands Monday |
| 2 | Branch 2 end-to-end once SendGrid key arrives; fill scorecard/pricing |
| 3 (half) | Demo rehearsal (record it); pre-read sent to BA/SA before the meeting |

**Monday dependency:** Branch 2's send-path demo needs the scoped SendGrid key (request text in doc 03). Everything else — scaffolding, Branch 1 retrieval work, docs, demo rehearsal — proceeds without waiting.
