using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class EndpointTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly bool _enabled;
        private readonly string? _previousConnection = Environment.GetEnvironmentVariable("SERVOERP_DATABASE_CONNECTION");
        private readonly string? _previousKey = Environment.GetEnvironmentVariable("SERVOERP_API_KEY");
        public Factory(bool enabled)
        {
            _enabled = enabled;
            Environment.SetEnvironmentVariable("SERVOERP_DATABASE_CONNECTION", "Server=127.0.0.1,1;Database=EngineerEndpointTest;Integrated Security=true;Connect Timeout=1;Encrypt=true");
            Environment.SetEnvironmentVariable("SERVOERP_API_KEY", "test-only-not-a-real-installation-key");
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Environment.SetEnvironmentVariable("SERVOERP_DATABASE_CONNECTION", _previousConnection);
            Environment.SetEnvironmentVariable("SERVOERP_API_KEY", _previousKey);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServoERP:DatabaseConnectionString"] = "Server=127.0.0.1,1;Database=EngineerEndpointTest;Integrated Security=true;Connect Timeout=1;Encrypt=true",
            ["ServoERP:ApiKey"] = "test-only-not-a-real-installation-key",
            ["ServoERP:EngineerMobileEnabled"] = _enabled.ToString()
        }));
    }
    [Fact] public async Task MobileEndpointsFailClosedWithoutCredentials()
    {
        using var factory = new Factory(true); using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var jobs = await client.GetAsync("/api/v1/mobile/jobs"); Assert.Equal(HttpStatusCode.Unauthorized, jobs.StatusCode);
        using var stock = await client.GetAsync("/api/v1/mobile/inventory/van"); Assert.Equal(HttpStatusCode.Unauthorized, stock.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", new LoginRequest("", "", 0, "")); Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new RefreshRequest("")); Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
    [Fact] public async Task MobileRoutesAreDisabledUnlessExplicitlyEnabled()
    {
        using var factory = new Factory(false); using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/api/v1/mobile/jobs"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
