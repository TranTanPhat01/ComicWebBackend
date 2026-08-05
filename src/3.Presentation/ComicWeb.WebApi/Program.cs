using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using ComicWeb.Application;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Persistence;
using ComicWeb.Persistence.Auth;
using ComicWeb.Persistence.Contexts;
using ComicWeb.WebApi.Auth;
using ComicWeb.WebApi.Infrastructure;
using ComicWeb.WebApi.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ConfigureCors(builder.Services);
ConfigureAuthentication(builder);
ConfigureAuthorization(builder.Services);
ConfigureRateLimiting(builder);
ConfigureApplicationServices(builder);
ConfigureControllers(builder.Services);
ConfigureSwagger(builder.Services);

var app = builder.Build();

await ApplyDatabaseMigrationsAsync(app);

ConfigureMiddleware(app);

await app.RunAsync();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    var settings = app.Configuration
        .GetSection(DatabaseInitializationOptions.SectionName)
        .Get<DatabaseInitializationOptions>()
        ?? new DatabaseInitializationOptions();

    if (!settings.ApplyMigrationsOnStartup)
    {
        return;
    }

    var logger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseInitializer");

    await app.Services.MigrateDatabaseAsync(logger);
}

static void ConfigureCors(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddPolicy(
            "NextJsPolicy",
            policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:3000",
                        "https://truyenweb.vercel.app",
                        "https://comic-web-front-end.vercel.app")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });
}

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var jwt = builder.Configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
        ?? throw new InvalidOperationException(
            "Jwt configuration is missing.");

    if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    {
        throw new InvalidOperationException(
            "Jwt signing key is missing.");
    }

    if (jwt.SigningKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt signing key is too short.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

            options.Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();

                    return ProblemResponse.WriteAsync(
                        context.HttpContext,
                        401,
                        "UNAUTHORIZED",
                        "Unauthorized",
                        "Authentication is required.");
                },

                OnForbidden = context =>
                {
                    return ProblemResponse.WriteAsync(
                        context.HttpContext,
                        403,
                        "FORBIDDEN",
                        "Forbidden",
                        "You do not have permission to access this resource.");
                }
            };
        });
}

static void ConfigureAuthorization(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        options.AddPolicy(
            "AdminOnly",
            policy =>
            {
                policy
                    .RequireAuthenticatedUser()
                    .RequireRole("Admin")
                    .RequireClaim("password_changed", "True")
                    .AddRequirements(new ActiveUserRequirement());
            });

        options.AddPolicy(
            "PasswordChanged",
            policy =>
            {
                policy
                    .RequireAuthenticatedUser()
                    .RequireClaim("password_changed", "True");
            });
    });
}

static void ConfigureRateLimiting(WebApplicationBuilder builder)
{
    var limits = builder.Configuration
        .GetSection(AuthenticationSecurityOptions.SectionName)
        .Get<AuthenticationSecurityOptions>()
        ?? new AuthenticationSecurityOptions();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(
            "login",
            context =>
            {
                var partitionKey =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.LoginRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

        options.AddPolicy(
            "refresh",
            context =>
            {
                var partitionKey =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.RefreshRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

        options.AddPolicy(
            "public-reading",
            context =>
            {
                var partitionKey =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PublicReadingRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
    });
}

static void ConfigureApplicationServices(
    WebApplicationBuilder builder)
{
    builder.Services.AddPersistenceServices(
        builder.Configuration);

    builder.Services.AddApplicationServices();

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddScoped<
        IAuthorizationHandler,
        ActiveUserHandler>();

    builder.Services.AddSingleton<LoginRequestRateLimiter>();

    builder.Services.AddSingleton<
        IAuthorizationMiddlewareResultHandler,
        ProblemDetailsAuthorizationResultHandler>();

    builder.Services.AddOptions<ScheduledPublishingOptions>()
        .Bind(builder.Configuration.GetSection(ScheduledPublishingOptions.SectionName))
        .Validate(options =>
            options.IntervalSeconds > 0 &&
            options.BatchSize is > 0 and <= 1000,
            "Scheduled publishing options are invalid: IntervalSeconds must be positive and BatchSize must be between 1 and 1000.")
        .ValidateOnStart();

    builder.Services.AddScoped<IScheduledPublishingService, ComicWeb.Application.Features.Stories.ScheduledPublishingService>();
    builder.Services.AddHostedService<ComicWeb.WebApi.Services.ScheduledPublishingWorker>();

    builder.Services.AddScoped<IAuditContextAccessor, ComicWeb.WebApi.Auth.AuditContextAccessor>();
    builder.Services.AddSingleton<IAuditDetailsSerializer, ComicWeb.Persistence.Logging.AuditDetailsSerializer>();
    builder.Services.AddScoped<IAuditWriter, ComicWeb.Persistence.Logging.AuditWriter>();
    builder.Services.AddScoped<IAuditLogQueryService, ComicWeb.Persistence.Logging.AuditLogQueryService>();

    builder.Services.AddMemoryCache();
    ConfigureResponseCompression(builder.Services);
}

static void ConfigureResponseCompression(IServiceCollection services)
{
    services.AddResponseCompression(options =>
    {
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        options.EnableForHttps = true;
        options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/json", "text/plain", "text/html" });
    });
}

static void ConfigureControllers(IServiceCollection services)
{
    services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization
                    .JsonStringEnumConverter());
        });

    services.AddEndpointsApiExplorer();
}

static void ConfigureSwagger(IServiceCollection services)
{
    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "ComicWeb API",
                Version = "v1",
                Description = "ComicWeb backend API"
            });

        options.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Nhập JWT access token. Không cần gõ tiền tố Bearer."
            });

        options.OperationFilter<AuthorizeOperationFilter>();

        var assemblyName =
            Assembly.GetExecutingAssembly()
                .GetName()
                .Name;

        var xmlPath =
            Path.Combine(
                AppContext.BaseDirectory,
                $"{assemblyName}.xml");

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });
}

static void ConfigureMiddleware(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(
                "/swagger/v1/swagger.json",
                "ComicWeb API v1");

            options.RoutePrefix = "swagger";
            options.DocumentTitle = "ComicWeb API";
            options.DisplayRequestDuration();
            options.EnableTryItOutByDefault();
        });
    }

    app.UseMiddleware<
        ComicWeb.WebApi.Middlewares.ExceptionHandlingMiddleware>();

    app.Use(
        async (context, next) =>
        {
            context.Response.Headers["X-Request-Id"] =
                context.TraceIdentifier;

            await next();
        });

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseResponseCompression();
    app.UseCors("NextJsPolicy");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
}

public partial class Program
{
}
