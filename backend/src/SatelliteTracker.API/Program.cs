using Microsoft.EntityFrameworkCore;
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

// Services
builder.Services.AddScoped<ITleService, TleService>();
builder.Services.AddScoped<IPassService, PassService>();

// N2YO HTTP client (reads N2YO:ApiKey from configuration)
builder.Services.AddHttpClient<IN2YOClient, N2YOClient>();

// Firebase
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();

// Calendar sync (ICS MVP; see OutlookServiceCollectionExtensions to swap to Graph)
builder.Services.AddOutlookServiceModule();

// Background jobs
builder.Services.AddHostedService<TleUpdateJob>();
builder.Services.AddHostedService<PassCalculationJob>();
builder.Services.AddHostedService<PassNotificationJob>();

// Caching
builder.Services.AddMemoryCache();

builder.Services.AddControllers();

builder.Services.AddSwaggerGen();

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
app.MapControllers();
app.Run();
