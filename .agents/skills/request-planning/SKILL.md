---
name: request-planning
description: Clarify user intent and produce an implementation-ready plan before coding. Use when a request changes architecture, authentication, database schema, tests, public UI, security posture, or when the user proposes an implementation method that needs trade-off analysis.
---

# Request Planning

Use this skill before implementation when the request has meaningful ambiguity or engineering trade-offs.

## Steps

1. Inspect relevant files and current behavior first.
2. Identify what is known, unknown, and risky.
3. Ask concise blocking questions only when repository context cannot answer them.
4. Explain the proposed implementation flow.
5. Include a trade-off table covering scalability, maintainability, security, performance, and user experience.
6. Use Mermaid for flows that are easier to reason about visually.
7. End with concrete acceptance criteria and verification commands.

## Defaults

- Prefer production-safe, maintainable choices over minimal demos.
- Keep `Razor/` as the presentation layer and preserve `ServiceLayer/` plus `DataAccessLayer/` boundaries.
- Choose framework-native ASP.NET Core and EF Core patterns unless the codebase proves otherwise.
