# Security Checklist

- Secrets and credentials absent from source.
- PII absent from logs and test artifacts.
- Auth middleware order correct.
- Protected endpoints require authorization.
- Forms are antiforgery-protected.
- EF queries are parameterized or LINQ-based.
- Redirects are local or validated.
