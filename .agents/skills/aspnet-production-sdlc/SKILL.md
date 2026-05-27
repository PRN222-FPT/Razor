---
name: aspnet-production-sdlc
description: Apply production-grade SDLC, architecture, security, testing, and handoff rules for this ASP.NET Core Razor Pages layered repository. Use when planning, implementing, reviewing, or handing off changes that affect Razor Pages, ServiceLayer, DataAccessLayer, EF Core, Repository, Unit of Work, SQL Server, Identity, tests, app configuration, or public web behavior.
---

# ASP.NET Production SDLC

Use this skill to keep `Razor/`, `ServiceLayer/`, and `DataAccessLayer/` aligned with production-grade engineering.

## Workflow

1. Inspect the current code before proposing changes.
2. Clarify only blocking product or assignment requirements.
3. State the implementation flow and a concise trade-off table when alternatives exist.
4. Implement a small vertical change with clear boundaries.
5. Add or update unit, integration, and Playwright tests according to risk.
6. Run feasible verification commands.
7. Perform a security and privacy review before handoff.
8. Summarize decisions, changed files, verification, risks, and next actions.

## Architecture Defaults

- Keep `Razor/` as the presentation layer unless the user explicitly asks for another web app.
- Use `DataAccessLayer` for EF Core entities, `AppDbContext`, repositories, and unit of work.
- Use ASP.NET Core Identity for local account authentication.
- Use EF Core async APIs and LINQ for data access.
- Keep Razor Page models thin; move reusable or security-sensitive logic into services.
- Use view/input models at UI boundaries to prevent over-posting.

## Quality Gates

- Preserve nullable reference types.
- Follow SOLID, KISS, and DRY pragmatically.
- Do not add abstractions that only hide two simple examples.
- Keep production secrets out of source.
- Verify with build and relevant tests. If a command cannot run, record the exact blocker.
