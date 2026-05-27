# ASP.NET Core Rule

Apply this rule when editing `.cs`, `.cshtml`, `.csproj`, or `appsettings*.json` files.

- Use ASP.NET Core built-in DI, configuration, logging, Identity, authorization, validation, and antiforgery.
- Keep Razor Page handlers async when they call I/O.
- Keep Razor Page models thin; move reusable logic into `ServiceLayer`.
- Use EF Core LINQ or parameterized SQL, never string-concatenated SQL.
- Keep EF Core `DbContext`, repositories, and unit-of-work implementation in `DataAccessLayer`.
- Keep test projects aligned with the Razor app and the layer they verify.
