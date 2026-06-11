using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;

var builder = WebApplication.CreateBuilder(args);

// Database configuration: The AddDbContext method is used to register the AppDbContext with the dependency injection container, allowing it to be injected into controllers and other services that require database access.
// The UseNpgsql method is called to configure the context to use PostgreSQL as the database provider,
// and the connection string is retrieved from the application's configuration settings using GetConnectionString("DefaultConnection").
builder.Services.AddDbContext<AppDbContext>(options =>options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers(); 

var app = builder.Build(); 

app.MapControllers();
app.Run();
