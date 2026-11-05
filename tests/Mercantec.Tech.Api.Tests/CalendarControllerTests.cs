using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class CalendarControllerTests
{
    [Fact]
    public async Task StudentsAreForbidden()
    {
        var controller = Controller(email: "elev@mercantec.dk", teacher: false);
        Assert.IsType<ForbidResult>(await controller.Mine(null, null, default));
    }

    [Fact]
    public async Task TeacherWithoutEmailGetsBadRequest()
    {
        var controller = Controller(email: null, teacher: true);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Mine(null, null, default));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("missing_email", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnconfiguredGraphReturnsEmptyConfiguredFalse()
    {
        var controller = Controller(email: "kasc@mercantec.dk", teacher: true, configured: false);
        var ok = Assert.IsType<OkObjectResult>(await controller.Mine(null, null, default));
        var view = Assert.IsType<TeacherCalendarView>(ok.Value);
        Assert.False(view.Configured);
        Assert.Equal("kasc@mercantec.dk", view.AzureUpn);
        Assert.Empty(view.Events);
    }

    private static CalendarController Controller(string? email, bool teacher, bool configured = false)
    {
        var opts = Options.Create(new AzureGraphOptions
        {
            TenantId = configured ? "t" : "",
            ClientId = configured ? "c" : "",
            ClientSecret = configured ? "s" : ""
        });
        // HttpClientFactory er ikke nødvendig når IsConfigured er false
        var graph = new AzureGraphCalendarClient(
            new EmptyHttpClientFactory(),
            opts,
            NullLogger<AzureGraphCalendarClient>.Instance);

        var claims = new List<Claim>();
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        if (email is not null) claims.Add(new("email", email));
        claims.Add(new("sub", Guid.NewGuid().ToString()));

        return new CalendarController(graph)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
