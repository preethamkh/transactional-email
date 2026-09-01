# Agent Build Brief: APC Transactional Email

## Objective

Build a production-ready Mandrill-backed transactional email capability for APC without embedding Mandrill credentials or provider-specific code in calling systems.

## Required outcome

- Versioned HTTP API for all systems
- Optional thin .NET client that calls the API only
- Mandrill adapter behind an interface
- Stable template keys mapped to Mandrill slugs
- Durable audit records and provider event correlation
- Retry, idempotency and dead-letter handling
- Support search UI with role-based access
- D365 and Power Automate integration specifications, but no direct changes without explicit assignment
- Terraform for isolated environments

## Constraints

- Never modify the APC Azure subscription without explicit approval.
- Never commit secrets, tokens, API keys or personal data.
- Keep Mailchimp marketing retrieval separate from Mandrill transactional sending.
- Do not add email audit tables to an unrelated application database.
- Prefer the smallest maintainable Azure design.

## Required sequence

1. Inspect repository, current email paths and deployment conventions.
2. Confirm the HTTP contract and ownership of template keys.
3. Implement contracts and provider interface.
4. Implement Mandrill provider with timeout, cancellation and safe error handling.
5. Implement API authentication, validation, idempotency and audit persistence.
6. Implement queue/worker/event handling.
7. Implement support search and Entra authorization.
8. Add D365/Power Automate integration documentation.
9. Add Terraform and deployment documentation.
10. Run build, tests, security review and secret scan.

## Verification required

- Unit tests for contract validation, provider payloads, auth, idempotency and event mapping.
- Integration tests using a fake provider and ephemeral database.
- Manual smoke test using a non-production Mandrill template and authenticated recipient.
- Failure test for timeout, rejected recipient and duplicate idempotency key.
- No tests or scripts may depend on APC production data.
