using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Dtos
{
    public record AuthResultDto(string Token, string Username, DateTime Expiration);
}
