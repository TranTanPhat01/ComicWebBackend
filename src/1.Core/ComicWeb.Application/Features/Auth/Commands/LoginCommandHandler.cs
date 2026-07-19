using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens; // Đổi sang cái này
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace ComicWeb.Application.Features.Auth.Commands
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public LoginCommandHandler(IApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResultDto?> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // 5. CHỈNH SỬA TẠI ĐÂY: Tìm user trong Database thay vì check chuỗi "admin"
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

            // Kiểm tra xem User có tồn tại không và verify mật khẩu hash qua BCrypt
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return null; // Sai tài khoản hoặc mật khẩu
            }

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
            var expiryMinutes = double.Parse(jwtSettings["ExpiryInMinutes"]!);
            var expiration = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var key = new SymmetricSecurityKey(secretKey);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Cấu hình mô tả Token theo thư viện mới
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, request.Username),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires = expiration,
                SigningCredentials = creds
            };

            // Dùng Handler mới để tạo chuỗi Token trực tiếp
            var tokenHandler = new JsonWebTokenHandler();
            var tokenString = tokenHandler.CreateToken(tokenDescriptor);

            return new AuthResultDto(tokenString, request.Username, expiration);
        }
    }
}