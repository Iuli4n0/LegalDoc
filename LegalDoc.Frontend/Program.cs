using LegalDoc.Frontend.Components;
using LegalDoc.Frontend.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var detailedErrors = builder.Configuration.GetValue("DetailedErrors", builder.Environment.IsDevelopment());
var apiTimeoutSeconds = builder.Configuration.GetValue("HttpClient:ApiTimeoutSeconds", 600);
var identityTimeoutSeconds = builder.Configuration.GetValue("HttpClient:IdentityTimeoutSeconds", 30);

// Add services to the container.
builder.Services.AddRazorComponents(options =>
    {
        options.DetailedErrors = detailedErrors;
    })
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Auth services
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<ProtectedLocalStorage>();
builder.Services.AddScoped<IAuthStorage, ProtectedLocalAuthStorage>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();

// HttpClient for IdentityService (no auth header needed for login/register)
builder.Services.AddHttpClient("IdentityAPI", client =>
{
    var identityUrl = builder.Configuration["IdentityServiceUrl"];
    if (string.IsNullOrWhiteSpace(identityUrl))
        throw new InvalidOperationException("IdentityServiceUrl missing in config");

    client.BaseAddress = new Uri(identityUrl);
    client.Timeout = TimeSpan.FromSeconds(identityTimeoutSeconds);
});

// HttpClient for DocumentService (auth header set by ApiClient at call time)
builder.Services.AddHttpClient("API", client =>
{
    var apiUrl = builder.Configuration["DocumentServiceUrl"];
    if (string.IsNullOrWhiteSpace(apiUrl))
        throw new InvalidOperationException("DocumentServiceUrl missing in config");

    client.BaseAddress = new Uri(apiUrl);
    client.Timeout = TimeSpan.FromSeconds(apiTimeoutSeconds);
});

// Configure persistent Data Protection keys for Blazor antiforgery/protected storage tokens so they remain decryptable across container restarts.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? "/app/.aspnet/DataProtection-Keys";

try
{
    Directory.CreateDirectory(dataProtectionKeysPath);
}
catch (Exception)
{
    dataProtectionKeysPath = Path.Combine(Path.GetTempPath(), "legaldoc-dpkeys");
    Directory.CreateDirectory(dataProtectionKeysPath);
}

builder.Services.AddDataProtection()
    .SetApplicationName("LegalDoc.Frontend")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

var useHttpsRedirection = builder.Configuration.GetValue("UseHttpsRedirection", !builder.Environment.IsDevelopment());
var configuredPathBase = NormalizePathBase(builder.Configuration["PathBase"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (useHttpsRedirection)
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
}

if (!string.IsNullOrEmpty(configuredPathBase))
{
    app.UsePathBase(configuredPathBase);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

static string NormalizePathBase(string? pathBase)
{
    if (string.IsNullOrWhiteSpace(pathBase))
    {
        return string.Empty;
    }

    var normalized = pathBase.Trim();
    if (!normalized.StartsWith('/'))
    {
        normalized = "/" + normalized;
    }

    return normalized.TrimEnd('/');
}
