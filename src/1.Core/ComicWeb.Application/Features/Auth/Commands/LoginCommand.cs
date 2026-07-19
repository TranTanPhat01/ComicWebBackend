using ComicWeb.Application.Dtos;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Auth.Commands
{
    // Request nhận vào tài khoản và mật khẩu từ Admin
    public record LoginCommand(string Username, string Password) : IRequest<AuthResultDto?>;
}