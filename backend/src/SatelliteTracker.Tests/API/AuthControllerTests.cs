using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Database.Security;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.API;

public class AuthControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _controller = new AuthController(new AllowlistedEmailRepository(_context), new ApiKeyRepository(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void Allowlist(string email)
    {
        _context.AllowlistedEmails.Add(new AllowlistedEmail
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            AddedAt = DateTimeOffset.UtcNow
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Register_EmailNotAllowlisted_Returns403()
    {
        var result = await _controller.Register(new RegisterRequest("nobody@iai.co.il", "Nobody"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_AllowlistedNoExistingKey_CreatesApiKeyWithMatchingHashAndReturnsRawKey()
    {
        Allowlist("tester@iai.co.il");

        var result = await _controller.Register(new RegisterRequest(" Tester@IAI.co.il ", "Tester"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegisterResponse>(ok.Value);
        Assert.NotEmpty(response.ApiKey);
        Assert.Equal("tester@iai.co.il", response.Email);
        Assert.Equal("Tester", response.DisplayName);

        var stored = Assert.Single(_context.ApiKeys);
        Assert.Equal("tester@iai.co.il", stored.Email);
        Assert.Equal("Tester", stored.DisplayName);
        Assert.True(stored.IsActive);
        Assert.Equal(ApiKeyHasher.Hash(response.ApiKey), stored.KeyHash);
    }

    [Fact]
    public async Task Register_AllowlistedNoExistingKey_DoesNotCreateUserSettings()
    {
        Allowlist("tester@iai.co.il");

        await _controller.Register(new RegisterRequest("tester@iai.co.il", "Tester"));

        Assert.Empty(_context.UserSettings);
    }

    [Fact]
    public async Task Register_AlreadyRegisteredWithActiveKey_Returns409()
    {
        Allowlist("tester@iai.co.il");
        await _controller.Register(new RegisterRequest("tester@iai.co.il", "Tester"));

        var result = await _controller.Register(new RegisterRequest("tester@iai.co.il", "Tester Again"));

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, _context.ApiKeys.Count());
    }

    [Fact]
    public async Task Register_RevokedExistingKey_AllowsReRegistration()
    {
        Allowlist("tester@iai.co.il");
        _context.ApiKeys.Add(new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = "tester@iai.co.il",
            DisplayName = "Old Device",
            KeyHash = new string('a', 64),
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _controller.Register(new RegisterRequest("tester@iai.co.il", "New Device"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, _context.ApiKeys.Count());
    }
}
