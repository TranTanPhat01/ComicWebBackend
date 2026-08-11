using ComicWeb.Application.Common.Interface;
using ComicWeb.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ComicWeb.Persistence.Auth;
using ComicWeb.Persistence.Content;
using Microsoft.Extensions.Options;

namespace ComicWeb.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(
            provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISlugGenerator, VietnameseSlugGenerator>();
        services.AddSingleton<IHtmlContentSanitizer, Content.HtmlContentSanitizer>();

        services.AddOptions<AuthenticationSecurityOptions>()
            .Bind(configuration.GetSection(AuthenticationSecurityOptions.SectionName))
            .Validate(
                options => options.MaxFailedLoginAttempts > 0
                    && options.LockoutMinutes > 0
                    && options.LoginRequestsPerMinute > 0
                    && options.LoginRequestsPerHour > 0
                    && options.RefreshRequestsPerMinute > 0,
                "Authentication security options are invalid.")
            .ValidateOnStart();

        services.AddSingleton<IAuthenticationSecurityPolicy, AuthenticationSecurityPolicy>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer)
                    && !string.IsNullOrWhiteSpace(options.Audience)
                    && options.SigningKey.Length >= 32
                    && options.AccessTokenMinutes is > 0 and <= 60,
                "Jwt configuration is invalid.")
            .ValidateOnStart();

        services.AddOptions<AdminBootstrapOptions>()
            .Bind(configuration.GetSection(AdminBootstrapOptions.SectionName));

        services.AddOptions<DatabaseInitializationOptions>()
            .Bind(configuration.GetSection(DatabaseInitializationOptions.SectionName))
            .Validate(
                options => options.MaxRetryAttempts > 0,
                "Database migration retry count must be greater than zero.")
            .Validate(
                options => options.RetryDelaySeconds > 0,
                "Database migration retry delay must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<ComicWeb.Application.Common.Configurations.PublicCacheOptions>()
            .Bind(configuration.GetSection(ComicWeb.Application.Common.Configurations.PublicCacheOptions.SectionName))
            .Validate(
                options => options.StoryListTtlSeconds > 0
                    && options.StoryDetailTtlSeconds > 0
                    && options.ChapterListTtlSeconds > 0
                    && options.ChapterDetailTtlSeconds > 0
                    && options.MaxEntrySizeBytes > 0,
                "Public cache TTL and MaxEntrySize must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<ComicWeb.Application.Common.Interfaces.IPublicContentCache, Caching.PublicContentCache>();
        services.AddSingleton<ComicWeb.Application.Common.Interfaces.IPublicCacheKeyFactory, Caching.PublicCacheKeyFactory>();
        services.AddSingleton<ComicWeb.Application.Common.Interfaces.IPublicContentCacheInvalidator, Caching.PublicContentCacheInvalidator>();
        services.AddSingleton<ComicWeb.Application.Common.Caching.KeyedLockManager>();

        services.AddHostedService<AdminBootstrapService>();
        services.AddHostedService<GenreSeedService>();

        return services;
    }
}
