using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Repositories;
using IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

// Map flat .env variables to ASP.NET configuration hierarchy
var envMappings = new Dictionary<string, string?>
{
    ["Stripe:SecretKey"] = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"),
    ["Stripe:WebhookSecret"] = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET"),
    ["Stripe:Prices:Bronze"] = Environment.GetEnvironmentVariable("STRIPE_PRICE_BRONZE"),
    ["Stripe:Prices:Silver"] = Environment.GetEnvironmentVariable("STRIPE_PRICE_SILVER"),
    ["Stripe:Prices:Gold"] = Environment.GetEnvironmentVariable("STRIPE_PRICE_GOLD"),
};

// Only add non-null values so appsettings defaults aren't overwritten with nulls
var filtered = envMappings.Where(kv => !string.IsNullOrEmpty(kv.Value))
    .ToDictionary(kv => kv.Key, kv => kv.Value);

if (filtered.Count > 0)
    builder.Configuration.AddInMemoryCollection(filtered!);

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
    cfg.RegisterServicesFromAssembly(typeof(IUserRepository).Assembly));

// EF Core - PostgreSQL (Separate database for Identity)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

// Application services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IStripeService, StripeService>();

// Stripe configuration
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

// JWT Authentication - read from configuration
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
Console.WriteLine($"[IdentityService] JWT Config: Issuer={jwtIssuer}, Audience={jwtAudience}, SecretLen={jwtSecret.Length}");

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Apply EF Core migrations automatically at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
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
        if (!System.IO.File.Exists(fullPath))
        {
            continue;
        }

        DotNetEnv.Env.Load(fullPath);
        return;
    }
}
