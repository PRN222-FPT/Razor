# FPT UniRAG Razor Pages

ASP.NET Core Razor Pages application for FPT UniRAG. The solution uses a layered structure:

- `Razor/`: presentation layer, Razor Pages, SignalR hubs, background host wiring
- `ServiceLayer/`: business logic, orchestration, DTOs, document processing, chat services
- `DataAccessLayer/`: EF Core entities, `AppDbContext`, repositories, unit of work
- `ServiceLayer.Tests/`: xUnit tests

## Architecture Diagram

![FPT UniRAG architecture](prn222.drawio%20(1).png)

## Main Features

- Cookie-based authentication with role-based access for `admin`, `teacher`, and `student`
- Admin portal for:
  - creating, updating, and deleting subjects
  - creating teacher accounts
  - importing student accounts from `.csv` or `.xlsx`
  - suspending, reactivating, and resetting managed accounts
- Teacher document management:
  - subject-scoped upload
  - processing queue and document status updates
  - realtime subject assignment/update/delete notifications via SignalR
- Student chat and document access
- Background document processing pipeline with chunking, embeddings, and vector search

## Solution Layout

```text
GROUP1_Ass2.slnx
├── Razor/
├── ServiceLayer/
├── DataAccessLayer/
└── ServiceLayer.Tests/
```

For more detail, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Prerequisites

- .NET SDK 10
- PostgreSQL
- Optional but expected for full RAG flow:
  - Qdrant
  - Gemini API key for embeddings
  - OpenRouter API key for answer generation
- Optional for email workflows:
  - SMTP credentials for student, teacher, and password reset emails

## Configuration

Primary configuration lives in:

- [Razor/appsettings.json](Razor/appsettings.json)
- [Razor/appsettings.Development.json](Razor/appsettings.Development.json)

Important sections:

- `ConnectionStrings:DefaultConnectionString`
- `DefaultAdmin`
- `DefaultStudent`
- `TeacherDocumentUpload`
- `DocumentProcessing`
- `StudentCredentialEmail`
- `Gemini`
- `OpenRouter`
- `Qdrant`
- `RagChat`

Notes:

- The app throws at startup if `ConnectionStrings:DefaultConnectionString` is missing.
- Startup tries to run database compatibility updates and seed default accounts.
- If the database is unavailable, those startup tasks are skipped with warnings instead of crashing the host.
- `TeacherDocumentUpload.StorageRootPath` and `DocumentProcessing.TessDataPath` are normalized relative to `Razor/` when not absolute.

## Default Development Accounts

The repository currently seeds these development accounts by default through `appsettings.json`:

- Admin: `admin@fpt.edu.vn`
- Student: `student@fpt.edu.vn`

Passwords and seed behavior are controlled by the `DefaultAdmin` and `DefaultStudent` sections. Review those values before sharing the environment.

## How To Run

1. Make sure PostgreSQL is running and the configured database exists.
2. Update `Razor/appsettings.json` for your local database and any API keys you want to use.
3. From the repo root, build the solution:

```powershell
dotnet build GROUP1_Ass2.slnx
```

4. Run the web app:

```powershell
dotnet run --project Razor/PresentationLayer.csproj
```

Default local URLs are controlled by [Razor/Properties/launchSettings.json](Razor/Properties/launchSettings.json).

## Realtime Behavior

The app uses SignalR hubs registered in `Razor/Program.cs`:

- `/hubs/document-processing`
- `/hubs/student-chat`

Current teacher realtime behavior includes:

- document processing status updates
- subject assigned notifications
- subject updated notifications
- subject deleted notifications

## Testing

Build:

```powershell
dotnet build GROUP1_Ass2.slnx
```

Run tests:

```powershell
dotnet test GROUP1_Ass2.slnx
```

## Notes For Developers

- The composition root is [Razor/Program.cs](Razor/Program.cs).
- Admin orchestration is centered in `ServiceLayer/Services/AdminUserService.cs`.
- Teacher realtime notifications use `SignalRTeacherSubjectRealtimeNotifier`.
- Teacher document storage defaults to `Razor/App_Data/Uploads`.
- The app uses EF Core with `Npgsql`.

## Related Docs

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [TEACHER_DOCUMENT_UPLOAD_FLOW.md](TEACHER_DOCUMENT_UPLOAD_FLOW.md)
- [AGENTS.md](AGENTS.md)
