---
name: test-strategy
description: Design and verify xUnit unit tests, ASP.NET Core integration tests, and Playwright UI tests for this repository. Use when adding features, changing behavior, touching Identity/EF Core, or preparing release/handoff verification.
---

# Test Strategy

Use this skill to keep coverage proportional to risk.

## Test Layers

- Unit tests: verify small deterministic logic without web host or database when possible.
- Integration tests: use `WebApplicationFactory` to verify routing, middleware, auth redirects, health endpoints, and server-side behavior.
- UI tests: use Playwright for browser-visible flows such as home page, register/login/logout navigation, protected content, and form validation.

## Rules

- Keep tests aligned to the layer under test: Razor Pages integration/UI tests, ServiceLayer unit tests, DataAccessLayer integration tests.
- Use isolated SQL Server test databases for integration flows that require persistence.
- Prefer deterministic test data and unique email addresses for Identity tests.
- Do not rely on test execution order.
- Keep browser tests small and smoke-oriented unless a workflow is business-critical.

## Verification

Run `dotnet build GROUP1_Ass2.slnx` and `dotnet test GROUP1_Ass2.slnx` when test projects exist. Run Playwright browser install before UI tests when needed.
