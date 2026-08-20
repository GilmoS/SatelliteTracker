using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.API;

// Exercises AdminController's endpoint logic directly against a real repository (SQLite
// in-memory) — X-Admin-Key enforcement itself is covered separately in
// RequireAdminKeyAttributeTests since filters don't run when a controller is invoked directly.
public class AdminControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _controller = new AdminController(new AllowlistedEmailRepository(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddToAllowlist_NewEmail_CreatesNormalizedRow()
    {
        var result = await _controller.AddToAllowlist(new AddAllowlistEmailRequest("  Tester@IAI.co.il  "));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AllowlistedEmailDto>(ok.Value);
        Assert.Equal("tester@iai.co.il", dto.Email);
        Assert.Equal(1, _context.AllowlistedEmails.Count());
    }

    [Fact]
    public async Task AddToAllowlist_DuplicateEmailDifferentCasing_IsIdempotent()
    {
        await _controller.AddToAllowlist(new AddAllowlistEmailRequest("tester@iai.co.il"));
        var result = await _controller.AddToAllowlist(new AddAllowlistEmailRequest("  Tester@IAI.co.il  "));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, _context.AllowlistedEmails.Count());
    }

    [Fact]
    public async Task GetAllowlist_ReturnsAllEntries()
    {
        await _controller.AddToAllowlist(new AddAllowlistEmailRequest("a@iai.co.il"));
        await _controller.AddToAllowlist(new AddAllowlistEmailRequest("b@iai.co.il"));

        var result = await _controller.GetAllowlist();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<AllowlistedEmailDto>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public void Reissue_Returns501NotImplemented()
    {
        var result = _controller.Reissue(new ReissueRequest("tester@iai.co.il"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, objectResult.StatusCode);
    }
}
