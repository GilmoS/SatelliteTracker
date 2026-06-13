using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.TLEService.Client;
using SatelliteTracker.TLEService.Jobs;
using SatelliteTracker.TLEService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// Background jobs
builder.Services.AddHostedService<TleUpdateJob>();
builder.Services.AddHostedService<PassCalculationJob>();

// Caching
builder.Services.AddMemoryCache();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();
