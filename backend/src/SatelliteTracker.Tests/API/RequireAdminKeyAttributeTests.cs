using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.Filters;
using Xunit;

namespace SatelliteTracker.Tests.API;

public class RequireAdminKeyAttributeTests
{
    private static ActionExecutingContext BuildContext(string? configuredKey, string? providedKey)
    {
        var services = new ServiceCollection();
        var configBuilder = new ConfigurationBuilder();
        if (configuredKey is not null)
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:ApiKey"] = configuredKey });
        services.AddSingleton<IConfiguration>(configBuilder.Build());

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (providedKey is not null)
            httpContext.Request.Headers["X-Admin-Key"] = providedKey;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_CorrectKey_CallsNext()
    {
        var context = BuildContext(configuredKey: "secret", providedKey: "secret");
        var attribute = new RequireAdminKeyAttribute();
        var nextCalled = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingKey_ReturnsUnauthorizedWithoutCallingNext()
    {
        var context = BuildContext(configuredKey: "secret", providedKey: null);
        var attribute = new RequireAdminKeyAttribute();

        await attribute.OnActionExecutionAsync(context, () => throw new InvalidOperationException("next should not be called"));

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WrongKey_ReturnsUnauthorizedWithoutCallingNext()
    {
        var context = BuildContext(configuredKey: "secret", providedKey: "wrong");
        var attribute = new RequireAdminKeyAttribute();

        await attribute.OnActionExecutionAsync(context, () => throw new InvalidOperationException("next should not be called"));

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoConfiguredKey_ReturnsUnauthorizedWithoutCallingNext()
    {
        var context = BuildContext(configuredKey: null, providedKey: "anything");
        var attribute = new RequireAdminKeyAttribute();

        await attribute.OnActionExecutionAsync(context, () => throw new InvalidOperationException("next should not be called"));

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    // AdminController applies both attributes at the class level, so this single check covers
    // all three admin routes (allowlist POST/GET, reissue POST). There's no WebApplicationFactory
    // / generated-OpenAPI-document test infrastructure in this project yet, so this is the
    // "simple assertion via the ApiExplorer configuration" the task calls out as the fallback.
    [Fact]
    public void AdminController_IsGatedByAdminKeyAndExcludedFromApiExplorer()
    {
        var controllerType = typeof(AdminController);

        Assert.NotNull(controllerType.GetCustomAttributes(typeof(RequireAdminKeyAttribute), inherit: false).FirstOrDefault());

        var ignoreApi = controllerType
            .GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), inherit: false)
            .Cast<ApiExplorerSettingsAttribute>()
            .FirstOrDefault();
        Assert.NotNull(ignoreApi);
        Assert.True(ignoreApi!.IgnoreApi);
    }
}
