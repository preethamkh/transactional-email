# FINDINGS — Branch 1 (Mailchimp / Option 2)

Fill every row during execution. Redact keys. Copy exact error bodies where relevant — they are decision evidence.

## Environment

| Item | Value |
|---|---|
| Mailchimp trial account created (date) | |
| Trial plan tier | Standard (14-day trial) |
| API key format confirmed `KEY-dc` | |
| Dummy template name / id | |
| Template builder used (classic / newer) | |

## Gotcha checklist (from doc 02 §6)

- [ ] `GET /templates/{id}` returned clean reusable HTML for the chosen builder type? Notes:
- [ ] Marketing templates vs Mandrill templates are separate stores — evidenced by (docs URL / response field):
- [ ] Merge tags render at send time (Mandrill), not retrieval — evidence:
- [ ] Mandrill licensing facts (plan requirement, block size, price, any trial allowance):
- [ ] Any API rate limits hit:

## Operations log summary

| Op | Result | Latency | Notes |
|---|---|---|---|
| List templates | | | |
| Get template | | | |
| Render (offline) | | | |
| Send via SendGrid | | | |

## Pricing row (for scorecard)

| Item | Finding |
|---|---|
| Mailchimp plan required for transactional | |
| Transactional block cost / size | |
| Reporting retention | |
| Verdict on "compare with SendGrid" feasibility without payment | |

## Conclusion (one paragraph)

Does Option 2's core assumption hold (approved templates retrievable and usable by other systems)? What friction was found? What remains untestable without Mandrill?
