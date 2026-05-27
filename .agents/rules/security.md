# Security Rule

Apply this rule for authentication, authorization, configuration, data access, logging, forms, and public web UI.

- Never commit production secrets or credentials.
- Never log passwords, tokens, cookies, reset links, raw connection strings, or PII.
- Keep CSRF protection enabled for form posts.
- Protect authenticated-only pages with `[Authorize]` or policies.
- Validate user input server-side even when client validation exists.
- Use secure Identity flows and do not implement password handling manually.
- Run a secret/PII scan before handoff.
