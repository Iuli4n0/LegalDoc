using System;
using System.Text;
using Amazon.S3;
using DocumentService.Application.Abstractions;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Application services
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();
builder.Services.AddScoped<IResumeGeneratorService, OllamaResumeService>();


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

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

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
        ValidIssuer = jwtSettings["Issuer"] ?? defaultIssuer,
        ValidAudience = jwtSettings["Audience"] ?? defaultAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});

builder.Services.AddAuthorization();

// HttpClient for IdentityService (cross-service communication for limit checks)
builder.Services.AddHttpClient("IdentityAPI", client =>
{
    var identityUrl = builder.Configuration["IdentityServiceUrl"] ?? "http://localhost:5164";
    client.BaseAddress = new Uri(identityUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Apply EF Core migrations automatically at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
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

await app.RunAsync();
