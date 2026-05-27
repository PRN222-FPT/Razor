# GROUP1_Ass2 Structure Reference

```text
GROUP1_Ass2.slnx
├── DataAccessLayer/
│   ├── Entities/
│   ├── Repositories/
│   ├── UnitOfWork/
│   └── AppDbContext.cs
├── ServiceLayer/
│   ├── DTOs/
│   ├── Interfaces/
│   └── Services/
└── Razor/
    ├── Middlewares/
    ├── Pages/
    ├── ViewModels/
    └── Program.cs
```

PageModels depend on service interfaces. Services depend on `IUnitOfWork`. Repositories and `AppDbContext` stay in `DataAccessLayer`.
