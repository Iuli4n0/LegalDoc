using System;
using System.Text;
using Amazon.S3;
using DocumentService.Application.Abstractions;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Infrastructure.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "AllowFrontend";
const string defaultIssuer = "LegalDoc";
const string defaultAudience = "LegalDoc";

// CORS - origins read from config (env var: CorsOrigins)
var corsOrigins = builder.Configuration["CorsOrigins"]
    ?? "http://localhost:5288,https://localhost:7205";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(IDocumentRepository).Assembly));

// AWS S3 Configuration
var awsOptions = builder.Configuration.GetAWSOptions();

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();

Console.WriteLine($"AWS Region: {awsOptions.Region?.SystemName ?? "default"}");
Console.WriteLine($"AWS Profile: {awsOptions.Profile ?? "default"}");
Console.WriteLine($"S3 Bucket: {builder.Configuration["AWS:BucketName"]}");


// EF Core - PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

// Application services
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();
builder.Services.AddScoped<IResumeGeneratorService, OllamaResumeService>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IQAService, OllamaQAService>();
builder.Services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
builder.Services.AddScoped<IDocumentMessageRepository, DocumentMessageRepository>();


builder.Services.AddHttpClient<IClauseExtractorService, OllamaClauseExtractionService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddHttpClient<IClauseClassificationService, ClauseClassificationService>((_, client) =>
{
    var classifierBaseUrl = builder.Configuration["Classifier:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(classifierBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddScoped<IClauseRepository, ClauseRepository>();

// JWT Authentication - read from configuration (supports both environment variables and appsettings)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret not configured. Set JwtSettings:Secret or JWT_SECRET environment variable.");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? jwtSettings["Issuer"]
    ?? defaultIssuer;
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? jwtSettings["Audience"]
    ?? defaultAudience;
Console.WriteLine($"[DocumentService] JWT Config: Issuer={jwtIssuer}, Audience={jwtAudience}, SecretLen={jwtSecret.Length}");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();

// HttpClient for IdentityService (cross-service communication for limit checks)
builder.Services.AddHttpClient("IdentityAPI", client =>
{
    var identityUrl = builder.Configuration["IdentityServiceUrl"]
        ?? (builder.Environment.IsDevelopment()
            ? "http://localhost:5164"
            : "http://identity-service:8080");

    client.BaseAddress = new Uri(identityUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Apply EF Core migrations automatically at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var useHttpsRedirection = builder.Configuration.GetValue("UseHttpsRedirection", !builder.Environment.IsDevelopment());

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);

static void LoadDotEnvIfPresent()
{
    var cwd = Directory.GetCurrentDirectory();
    var candidates = new[]
    {
        Path.Combine(cwd, ".env"),
        Path.Combine(cwd, "..", ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env")
    };

    foreach (var candidate in candidates)
    {
        var fullPath = Path.GetFullPath(candidate);
        if (!File.Exists(fullPath))
        {
            continue;
        }

        DotNetEnv.Env.Load(fullPath);
        return;
    }
}
