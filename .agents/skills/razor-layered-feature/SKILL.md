---
name: razor-layered-feature
description: Extend or review GROUP1_Ass2 Razor Pages features that must follow the existing layered architecture. Use when adding CRUD pages, page handlers, view models, DTOs, services, repositories, unit-of-work members, EF Core entities, middleware, or architecture docs in this repository.
---

# Razor Layered Feature

Use this skill when changing a feature that crosses `Razor/`, `ServiceLayer/`, or `DataAccessLayer/`.

## Workflow

1. Read `AGENTS.md` and `ARCHITECTURE.md` first.
2. Identify the vertical slice: entity, DTOs, service interface, service implementation, view models, Razor Pages.
3. Keep dependency direction strict:
   - `Razor` -> `ServiceLayer`
   - `ServiceLayer` -> `DataAccessLayer`
   - `DataAccessLayer` -> no higher layer
4. Put EF Core entities, `DbSet` definitions, Fluent API, repositories, and `IUnitOfWork` members in `DataAccessLayer`.
5. Put business validation, orchestration, and entity-to-DTO mapping in `ServiceLayer`.
6. Put form-specific validation attributes and UI select-list state in `Razor/ViewModels`.
7. Keep Razor PageModels limited to binding, ModelState validation, DTO mapping, service calls, and navigation results.
8. Register new services in `Razor/Program.cs` with scoped lifetime.
9. Run `dotnet build GROUP1_Ass2.slnx`; run tests if a test project exists.

## Extension Checklist

For a new aggregate such as `Order`:

- Add `DataAccessLayer/Entities/Order.cs`.
- Add `DbSet<Order>` and Fluent API configuration to `AppDbContext`.
- Add `IRepository<Order> Orders` to `IUnitOfWork` and lazy initialization in `UnitOfWork`.
- Add DTO records under `ServiceLayer/DTOs`.
- Add `IOrderService` and `OrderService`.
- Add view models under `Razor/ViewModels`.
- Add Razor Pages under `Razor/Pages/Orders`.
- Update navigation only when the feature is user-facing.
- Update `ARCHITECTURE.md` if the dependency graph or conventions change.

## Rules

- Do not bind EF entities directly to Razor forms.
- Do not inject `AppDbContext` into PageModels.
- Do not call `SaveChangesAsync` from repositories; commit through `IUnitOfWork`.
- Prefer `AsNoTracking()` for read-only service queries.
- Use `Query()` only when a service needs composition such as `Include`, `Where`, `OrderBy`, or `AnyAsync`.
