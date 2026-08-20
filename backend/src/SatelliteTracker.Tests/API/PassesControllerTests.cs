using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.PassService.Services;
using Xunit;

namespace SatelliteTracker.Tests.API;

public class PassesControllerTests
{
    [Fact]
    public void PatchNotify_Returns501NotImplemented()
    {
        var controller = new PassesController(Mock.Of<IPassService>());

        var result = controller.PatchNotify(Guid.NewGuid(), new PatchNotifyRequest(true));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, objectResult.StatusCode);
    }
}
