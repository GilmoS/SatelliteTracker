using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.Jobs;
using SatelliteTracker.API.Services;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.OutlookService.Extensions;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.TLEService.Client;
using SatelliteTracker.TLEService.Jobs;
using SatelliteTracker.TLEService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ObserverSettings>(builder.Configuration.GetSection("Observer"));

// Database context
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<ISatelliteRepository, SatelliteRepository>();
builder.Services.AddScoped<ITleRepository, TleRepository>();
builder.Services.AddScoped<IPassRepository, PassRepository>();
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
builder.Services.AddScoped<IPassSubscriptionRepository, PassSubscriptionRepository>();
builder.Services.AddScoped<IPassNotificationLogRepository, PassNotificationLogRepository>();
builder.Services.AddScoped<IAllowlistedEmailRepository, AllowlistedEmailRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

// Services
builder.Services.AddScoped<ITleService, TleService>();
builder.Services.AddScoped<IPassService, PassService>();

// N2YO HTTP client (reads N2YO:ApiKey from configuration)
builder.Services.AddHttpClient<IN2YOClient, N2YOClient>();

// Firebase
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();

// Tester authentication (Milestone E, Step 1.3) — X-Api-Key header, see
// SatelliteTracker.API.Authentication.ApiKeyAuthenticationHandler. Registered as the default
// scheme so plain [Authorize] works without per-action scheme configuration. Deliberately
// separate from the admin X-Admin-Key mechanism (RequireAdminKeyAttribute) — see CLAUDE.md.
builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, _ => { });
builder.Services.AddAuthorization();

// Calendar sync (ICS MVP; see OutlookServiceCollectionExtensions to swap to Graph)
builder.Services.AddOutlookServiceModule();

// Background jobs
builder.Services.AddHostedService<TleUpdateJob>();
builder.Services.AddHostedService<PassCalculationJob>();
builder.Services.AddHostedService<PassNotificationJob>();

// Caching
builder.Services.AddMemoryCache();

builder.Services.AddControllers();

// SwaggerDoc metadata is required for the "v1" document name used by the
// `swagger tofile` CLI export that produces openapi/sattrakk-api.json (see
// openapi/README.md) — the Android app's OpenAPI Generator Gradle plugin
// consumes that static file to generate its DTO layer.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SatTrakk API",
        Version = "v1",
        Description = "Backend API for the Satellite Pass Tracker. Web and Android clients talk only to this API; " +
                       "it is the sole caller of N2YO and (in the future) Microsoft Graph."
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader());
});



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();   
}

app.UseCors("DevFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposed so SatelliteTracker.Tests can boot the real host via WebApplicationFactory<Program>.
public partial class Program { }
