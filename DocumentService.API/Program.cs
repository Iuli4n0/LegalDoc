using Amazon.S3;
using DocumentService.Application.Interfaces;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(DocumentService.Application.Interfaces.IDocumentRepository).Assembly));

// AWS S3 Configuration
var awsOptions = builder.Configuration.GetAWSOptions();

// Support for LocalStack (local development)
var serviceUrl = builder.Configuration["AWS:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
{
    awsOptions.DefaultClientConfig.ServiceURL = serviceUrl;
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);
    Console.WriteLine($"AWS configured for LocalStack at: {serviceUrl}");
}

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();

Console.WriteLine($"AWS Region: {awsOptions.Region?.SystemName ?? "default"}");
Console.WriteLine($"AWS Profile: {awsOptions.Profile ?? "default"}");
Console.WriteLine($"S3 Bucket: {builder.Configuration["AWS:BucketName"]}");


// EF Core - PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Application services
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
