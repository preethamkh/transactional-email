# FINDINGS — Branch 2 (Central Email Service / Option 4)

Fill during execution. Redact keys. This branch doubles as the SendGrid-incumbent evidence track.

## Environment

| Item | Value |
|---|---|
| SendGrid plan tier + monthly volume (from manager) | |
| Scoped key obtained (scopes granted) | |
| Sender identity verified | |
| Dynamic template created (id prefix d-...) | |
| Domain authentication status (SPF/DKIM) | |

## Verified behaviours

- [ ] `/health` responds with no configuration
- [ ] Unauthenticated calls rejected 401
- [ ] Send via Dynamic Template succeeded (202 Accepted)
- [ ] Template edited in SendGrid UI → resend reflects change without redeploy (record video)
- [ ] Unknown templateKey rejected
- [ ] Activity log captures send + webhook events
- [ ] Event webhook receives delivery/open events (direct POST or tunnel)

## SendGrid capability notes (incumbent evidence)

| Capability | Free/current tier? | Notes |
|---|---|---|
| Dynamic Templates + handlebars | | |
| Design Editor usability for business users | | |
| Versioning/rollback of templates | | |
| Event Webhook | | |
| Email Activity search/history | | |
| Verified sender / domain auth | | |

## Pricing row (for scorecard)

| Item | Finding |
|---|---|
| Incremental licence cost for this approach | Expected ~$0 — confirm tier covers volume |
| Tier upgrade needed for reporting history? | |
| Estimated monthly volume impact | |

## Conclusion (one paragraph)

Does the central-service shape hold against BRD FR-001/003/004/005/007? What surprised you? What is production work vs POC work?
