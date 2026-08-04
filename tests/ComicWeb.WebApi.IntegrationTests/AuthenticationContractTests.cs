using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Text.Json;

namespace ComicWeb.WebApi.IntegrationTests;

public sealed class AuthenticationContractTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;
    public AuthenticationContractTests(TestApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthorized_endpoint_returns_problem_details_with_request_id()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", body);
        Assert.Contains("requestId", body);
    }

    [Fact]
    public async Task Swagger_document_exposes_bearer_scheme_and_auth_paths_in_development()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ComicWeb API", body);
        Assert.Contains("/api/v1/auth/login", body);
        Assert.Contains("/api/v1/auth/me", body);
        Assert.Contains("/api/v1/admin/stories", body);
        Assert.Contains("Bearer", body);

        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        Assert.False(
            paths.GetProperty("/api/v1/stories")
                .GetProperty("get")
                .TryGetProperty("security", out _));

        Assert.True(
            paths.GetProperty("/api/v1/admin/stories")
                .GetProperty("get")
                .TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Admin_story_endpoint_rejects_anonymous_call_with_problem_details()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/stories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
        builder.UseSetting("Jwt:SigningKey", "integration-test-key-only-not-a-deployment-secret-0001");
        builder.UseSetting("BootstrapAdmin:Enabled", "false");
        builder.UseSetting("DatabaseInitialization:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=not_used");
    }
}
