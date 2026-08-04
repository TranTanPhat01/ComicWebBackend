using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComicWeb.WebApi.IntegrationTests;

public sealed class PostgreSqlApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("BootstrapAdmin:Enabled", "false");
        builder.UseSetting("DatabaseInitialization:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Jwt:Issuer", JwtTestTokenFactory.Issuer);
        builder.UseSetting("Jwt:Audience", JwtTestTokenFactory.Audience);
        builder.UseSetting("Jwt:SigningKey", "postgresql-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("PublicCache:Enabled", "false");
    }
}
