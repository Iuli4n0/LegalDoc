using LegalDoc.Frontend.Components;
using LegalDoc.Frontend.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Auth services
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();

// HttpClient for IdentityService (no auth header needed for login/register)
builder.Services.AddHttpClient("IdentityAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["IdentityServiceUrl"] ?? "http://localhost:5164");
});

// HttpClient for DocumentService (auth header set by ApiClient at call time)
builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DocumentServiceUrl"] ?? "http://localhost:5163");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();