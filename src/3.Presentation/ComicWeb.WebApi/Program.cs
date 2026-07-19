using ComicWeb.Application;
using ComicWeb.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình CORS cho Next.js Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("NextJsPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://truyenweb.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Cấu hình Xác thực JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };
});

// 1. Tích hợp tầng Persistence (EF Core + PostgreSQL) vào hệ thống
builder.Services.AddPersistenceServices(builder.Configuration);

// Tích hợp tầng Application (MediatR + AutoMapper)
builder.Services.AddApplicationServices();

// 2. Kích hoạt hỗ trợ định tuyến theo mô hình Controller
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Trả về enum dạng string ("Ongoing") thay vì số (1)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Policy phân quyền Admin
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Cấu hình OpenAPI (Swagger thế hệ mới của .NET 9)
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Mẹo nhỏ cho .NET 9: Nếu bạn muốn có giao diện UI trực quan để test API như Swagger cũ,
    // hãy giữ nguyên hoặc sau này ta sẽ cài thêm gói Swashbuckle/Scalar sau nhé.
}
// Đăng ký Middleware bắt lỗi toàn cục ngay đầu pipeline
app.UseMiddleware<ComicWeb.WebApi.Middlewares.ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

// CORS phải đặt TRƯỚC Authentication/Authorization
app.UseCors("NextJsPolicy");

// Kích hoạt phân quyền (Bắt buộc phải có trước MapControllers để dùng JWT sau này)
app.UseAuthentication();
app.UseAuthorization();

// 3. Map các Routes từ các file Controller trong thư mục Controllers
app.MapControllers();

app.Run();