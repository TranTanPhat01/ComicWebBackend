using System.Net;
using System.Text.Json;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class SwaggerContractTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlApiFactory? _factory;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _factory = new PostgreSqlApiFactory(fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Swagger_matches_public_and_admin_security_contracts()
    {
        using var client = _factory!.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var publicPaths = new[]
        {
            "/api/v1/stories",
            "/api/v1/stories/{storySlug}",
            "/api/v1/stories/{storySlug}/chapters",
            "/api/v1/stories/{storySlug}/chapters/{chapterSlug}"
        };

        foreach (var path in publicPaths)
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            Assert.False(operation.TryGetProperty("security", out _));
        }

        var adminPaths = paths.EnumerateObject()
            .Where(path => path.Name.StartsWith("/api/v1/admin/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(adminPaths);

        foreach (var path in adminPaths)
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Name is "parameters")
                {
                    continue;
                }

                Assert.True(operation.Value.TryGetProperty("security", out var security));
                Assert.NotEqual(JsonValueKind.Null, security.ValueKind);
            }
        }

        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        AssertRouteParameter(paths, "/api/v1/stories/{storySlug}", "get", "storySlug");
        AssertRouteParameter(paths, "/api/v1/stories/{storySlug}/chapters/{chapterSlug}", "get", "chapterSlug");

        // Verify no duplicate operationId values exist
        var operationIds = new List<string>();
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                if (method.Name == "parameters") continue;
                if (method.Value.TryGetProperty("operationId", out var opIdProp))
                {
                    var opId = opIdProp.GetString();
                    if (opId != null)
                    {
                        operationIds.Add(opId);
                    }
                }
            }
        }
        var duplicateOpIds = operationIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicateOpIds);

        // Verify all enum schemas in components.schemas are of type string
        if (document.RootElement.TryGetProperty("components", out var components) &&
            components.TryGetProperty("schemas", out var schemas))
        {
            foreach (var schema in schemas.EnumerateObject())
            {
                if (schema.Value.TryGetProperty("enum", out _))
                {
                    Assert.True(schema.Value.TryGetProperty("type", out var typeProp));
                    Assert.Equal("string", typeProp.GetString());
                }
            }
        }
    }

    private static void AssertRouteParameter(JsonElement paths, string path, string method, string name)
    {
        var parameters = paths.GetProperty(path).GetProperty(method).GetProperty("parameters");
        Assert.Contains(
            parameters.EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == name
                && parameter.GetProperty("in").GetString() == "path"
                && parameter.GetProperty("required").GetBoolean());
    }
}
