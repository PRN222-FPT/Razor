---
name: secure-review
description: Review GROUP1_Ass2 ASP.NET Core Razor Pages layered changes for security, privacy, and secret-handling issues. Use before handoff or when touching Identity, authentication, authorization, EF Core queries, forms, logging, configuration, cookies, redirects, middleware, repositories, or user data.
---

# Secure Review

Use this skill as a gate before handoff for any security-relevant change.

## Checklist

- Confirm no production secrets, tokens, passwords, API keys, or privileged connection strings are stored in source.
- Confirm no PII is added to logs, test data, screenshots, or docs.
- Confirm `UseAuthentication()` runs before `UseAuthorization()`.
- Confirm protected routes and pages require `[Authorize]` or equivalent policies.
- Confirm form posts use antiforgery protection.
- Confirm user input is server-side validated.
- Confirm Razor output remains encoded by default.
- Confirm EF Core queries use LINQ or parameters, not string-concatenated SQL.
- Confirm redirects are local or explicitly validated.
- Confirm errors expose no stack traces outside development.

## Output

Report findings first, ordered by severity. Include file references, risk, and concrete remediation. If no issues are found, say so and list residual test gaps.
