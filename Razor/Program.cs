using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Net.Sockets;
using Razor.Hubs;
using Razor.Infrastructure;
using Razor.Middlewares;
using Razor.Services;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Student", "StudentOnly");
    options.Conventions.AuthorizeFolder("/Teacher", "TeacherOnly");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});
builder.Services.AddSignalR();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "FptAcademicAssistant.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (!IsHtmlRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);

            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("student"));
    options.AddPolicy("TeacherOnly", policy => policy.RequireRole("teacher"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.Configure<DefaultAdminOptions>(
    builder.Configuration.GetSection(DefaultAdminOptions.SectionName));
builder.Services.Configure<DefaultStudentOptions>(
    builder.Configuration.GetSection(DefaultStudentOptions.SectionName));
builder.Services.Configure<TeacherDocumentUploadOptions>(
    builder.Configuration.GetSection(TeacherDocumentUploadOptions.SectionName));
builder.Services.Configure<DocumentProcessingOptions>(
    builder.Configuration.GetSection(DocumentProcessingOptions.SectionName));
builder.Services.Configure<StudentCredentialEmailOptions>(
    builder.Configuration.GetSection(StudentCredentialEmailOptions.SectionName));
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<OpenRouterOptions>(
    builder.Configuration.GetSection(OpenRouterOptions.SectionName));
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection(QdrantOptions.SectionName));
builder.Services.Configure<RagChatOptions>(
    builder.Configuration.GetSection(RagChatOptions.SectionName));
builder.Services.PostConfigure<TeacherDocumentUploadOptions>(options =>
{
    options.StorageRootPath = string.IsNullOrWhiteSpace(options.StorageRootPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Uploads")
        : options.StorageRootPath;

    if (!Path.IsPathRooted(options.StorageRootPath))
    {
        options.StorageRootPath = Path.Combine(builder.Environment.ContentRootPath, options.StorageRootPath);
    }
});
builder.Services.PostConfigure<DocumentProcessingOptions>(options =>
{
    options.ChunkSize = Math.Max(200, options.ChunkSize);
    options.ChunkOverlap = Math.Clamp(options.ChunkOverlap, 0, options.ChunkSize / 2);
    options.Separators = options.Separators is { Length: > 0 }
        ? options.Separators
        : ["\r\n", "\n\n", "\n", " ", string.Empty];
    options.QueuePollIntervalSeconds = Math.Max(5, options.QueuePollIntervalSeconds);
    options.MaxEmbeddingBatchSize = Math.Max(1, options.MaxEmbeddingBatchSize);
    options.MinimumEmbeddedTextCharacters = Math.Max(0, options.MinimumEmbeddedTextCharacters);
    options.OcrLanguages = string.IsNullOrWhiteSpace(options.OcrLanguages)
        ? "eng+vie"
        : options.OcrLanguages.Trim();
    options.PdfOcrRenderDpi = Math.Clamp(options.PdfOcrRenderDpi, 72, 600);
    options.MaxOcrPages = Math.Max(1, options.MaxOcrPages);
    options.TessDataPath = string.IsNullOrWhiteSpace(options.TessDataPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "tessdata")
        : options.TessDataPath;

    if (!Path.IsPathRooted(options.TessDataPath))
    {
        options.TessDataPath = Path.Combine(builder.Environment.ContentRootPath, options.TessDataPath);
    }
});
builder.Services.PostConfigure<StudentCredentialEmailOptions>(options =>
{
    options.Host = options.Host?.Trim() ?? string.Empty;
    options.Username = options.Username?.Trim();
    options.Password = options.Password ?? string.Empty;
    options.SenderEmail = options.SenderEmail?.Trim() ?? string.Empty;
    options.SenderName = string.IsNullOrWhiteSpace(options.SenderName)
        ? "FPT UniRAG"
        : options.SenderName.Trim();
    options.Subject = string.IsNullOrWhiteSpace(options.Subject)
        ? "Your FPT UniRAG student account"
        : options.Subject.Trim();
});
builder.Services.PostConfigure<GeminiOptions>(options =>
{
    options.EmbeddingModel = string.IsNullOrWhiteSpace(options.EmbeddingModel)
        ? "gemini-embedding-2"
        : options.EmbeddingModel.Trim();
    options.OutputDimensionality = options.OutputDimensionality <= 0
        ? 768
        : options.OutputDimensionality;
});
builder.Services.PostConfigure<OpenRouterOptions>(options =>
{
    options.Model = string.IsNullOrWhiteSpace(options.Model)
        ? "openai/gpt-4o-mini"
        : options.Model.Trim();
    options.Temperature = Math.Clamp(options.Temperature, 0, 2);
    options.MaxOutputTokens = Math.Clamp(options.MaxOutputTokens, 16, 4000);
    options.AppName = string.IsNullOrWhiteSpace(options.AppName)
        ? "FPT UniRAG"
        : options.AppName.Trim();
    options.SiteUrl = options.SiteUrl?.Trim() ?? string.Empty;
    options.Timeout = options.Timeout <= TimeSpan.Zero
        ? TimeSpan.FromMinutes(5)
        : options.Timeout;
});
builder.Services.PostConfigure<QdrantOptions>(options =>
{
    options.Endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
        ? "http://localhost:6333"
        : options.Endpoint.TrimEnd('/');
    options.CollectionName = string.IsNullOrWhiteSpace(options.CollectionName)
        ? "document_chunks"
        : options.CollectionName.Trim();
});
builder.Services.PostConfigure<RagChatOptions>(options =>
{
    options.SearchLimit = Math.Clamp(options.SearchLimit, 1, 20);
    options.MinimumScore = Math.Clamp(options.MinimumScore, 0, 1);
    options.MaxQuestionLength = Math.Clamp(options.MaxQuestionLength, 100, 4000);
    options.MaxContextCharacters = Math.Clamp(options.MaxContextCharacters, 1000, 24000);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is not configured. Set ConnectionStrings:DefaultConnectionString.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<ITeacherDocumentService, TeacherDocumentService>();
builder.Services.AddScoped<IStudentDocumentService, StudentDocumentService>();
builder.Services.AddScoped<IDefaultAdminSeeder, DefaultAdminSeeder>();
builder.Services.AddScoped<IDefaultStudentSeeder, DefaultStudentSeeder>();
builder.Services.AddScoped<IDatabaseCompatibilityService, DatabaseCompatibilityService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IStudentCredentialEmailSender, SmtpStudentCredentialEmailSender>();
builder.Services.AddScoped<ITeacherCredentialEmailSender, SmtpTeacherCredentialEmailSender>();
builder.Services.AddScoped<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
builder.Services.AddScoped<IDocumentProcessingNotifier, SignalRDocumentProcessingNotifier>();
builder.Services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();
builder.Services.AddScoped<IDocumentParser, DocumentParser>();
builder.Services.AddScoped<IDocumentChunker, DocumentChunker>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IRagChatService, RagChatService>();
builder.Services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
});
builder.Services.AddHttpClient<IAnswerGenerationService, OpenRouterAnswerService>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/");
}).ConfigureHttpClient((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
    OpenRouterHttpClientConfiguration.Configure(client, options);
});
builder.Services.AddHttpClient<IVectorStore, QdrantVectorStore>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
    client.BaseAddress = new Uri($"{options.Endpoint}/");
});
builder.Services.AddHostedService<DocumentProcessingWorker>();

var app = builder.Build();

await ApplyDatabaseCompatibilityAsync(app);
await SeedDefaultAdminAsync(app);
await SeedDefaultStudentAsync(app);

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHub<DocumentProcessingHub>("/hubs/document-processing");
app.MapHub<StudentChatHub>("/hubs/student-chat");

app.Run();

static async Task SeedDefaultAdminAsync(WebApplication app)
{
    await RunDatabaseStartupTaskAsync(
        app,
        "Default admin seeding",
        async scope =>
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDefaultAdminSeeder>();
            await seeder.SeedAsync();
        });
}

static async Task SeedDefaultStudentAsync(WebApplication app)
{
    await RunDatabaseStartupTaskAsync(
        app,
        "Default student seeding",
        async scope =>
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDefaultStudentSeeder>();
            await seeder.SeedAsync();
        });
}

static async Task ApplyDatabaseCompatibilityAsync(WebApplication app)
{
    await RunDatabaseStartupTaskAsync(
        app,
        "Database compatibility updates",
        async scope =>
        {
            var compatibilityService = scope.ServiceProvider.GetRequiredService<IDatabaseCompatibilityService>();
            await compatibilityService.ApplyAsync();
        });
}

static async Task RunDatabaseStartupTaskAsync(
    WebApplication app,
    string operationName,
    Func<IServiceScope, Task> operation)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        await operation(scope);
    }
    catch (Exception ex) when (IsDatabaseUnavailable(ex))
    {
        app.Logger.LogWarning(
            ex,
            "{OperationName} was skipped because the database is not available.",
            operationName);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "{OperationName} failed during startup.",
            operationName);

        throw;
    }
}

static bool IsHtmlRequest(HttpRequest request)
{
    return request.Headers.Accept.Any(
        value => value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
}

static bool IsDatabaseUnavailable(Exception exception)
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (current is DbException or SocketException or TimeoutException)
        {
            return true;
        }
    }

    return false;
}
