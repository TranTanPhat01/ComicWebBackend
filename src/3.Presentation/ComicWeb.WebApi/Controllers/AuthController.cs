using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Auth;
using ComicWeb.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ComicWeb.WebApi.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(IMediator mediator, ICurrentUser currentUser, ComicWeb.WebApi.Auth.LoginRequestRateLimiter loginRateLimiter) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request) { loginRateLimiter.Check(Ip, request.UsernameOrEmail, DateTime.UtcNow); var r = await mediator.Send(new LoginCommand(request.UsernameOrEmail, request.Password, Ip, Request.Headers.UserAgent)); SetCookie(r.RefreshToken, r.RefreshExpiresAt); return Ok(new ApiEnvelope<LoginResponse>(r.Response, RequestId)); }
    [HttpGet("me"), Authorize] public async Task<IActionResult> Me() { var id = currentUser.UserId ?? throw new UnauthorizedAccessException(); return Ok(new ApiEnvelope<MeResponse>(await mediator.Send(new GetMeQuery(id)), RequestId)); }
    [HttpPost("refresh"), EnableRateLimiting("refresh")] public async Task<IActionResult> Refresh() { var raw = Request.Cookies["comicweb_refresh"]; if (string.IsNullOrEmpty(raw)) throw new ComicWeb.Application.Common.Exceptions.AppException("REFRESH_TOKEN_INVALID", 401, "Authentication failed", "Phiên đăng nhập không hợp lệ."); var r = await mediator.Send(new RefreshCommand(raw, Ip, Request.Headers.UserAgent)); SetCookie(r.RefreshToken, r.RefreshExpiresAt); return Ok(new ApiEnvelope<object>(new { accessToken = r.AccessToken, expiresIn = r.ExpiresIn }, RequestId)); }
    [HttpPost("logout")] public async Task<IActionResult> Logout() { await mediator.Send(new LogoutCommand(Request.Cookies["comicweb_refresh"], Ip)); DeleteCookie(); return NoContent(); }
    [HttpPost("change-password"), Authorize] public async Task<IActionResult> Change(ChangePasswordRequest r) { var id = currentUser.UserId ?? throw new UnauthorizedAccessException(); await mediator.Send(new ChangePasswordCommand(id, r.CurrentPassword, r.NewPassword, r.ConfirmPassword, Ip)); DeleteCookie(); return NoContent(); }
    private string RequestId => HttpContext.TraceIdentifier; private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private void SetCookie(string value, DateTime expires) => Response.Cookies.Append("comicweb_refresh", value, new CookieOptions { HttpOnly = true, Secure = !HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Lax, Path = "/api/v1/auth", Expires = new DateTimeOffset(expires), IsEssential = true });
    private void DeleteCookie() => Response.Cookies.Delete("comicweb_refresh", new CookieOptions { Path = "/api/v1/auth", Secure = !HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Lax });
}
