# Always-On Project Rules

Use `AGENTS.md` as the canonical source of truth for this repository.

Always follow these constraints:

- Treat `Razor/` as the presentation layer for this repository; `ServiceLayer/` and `DataAccessLayer/` are the lower layers.
- Apply production-grade SDLC, security, testing, and code-quality expectations.
- Ask blocking questions before implementation when product intent is unclear.
- Explain trade-offs when proposing or evaluating an implementation approach.
- Do not hardcode secrets, credentials, tokens, API keys, PII, or production connection strings.
- Do not initialize Git or add `.gitignore` unless the user explicitly requests it.
