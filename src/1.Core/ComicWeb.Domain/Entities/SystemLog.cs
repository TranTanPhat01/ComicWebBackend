using ComicWeb.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Domain.Entities
{
    public class SystemLog : BaseEntity
    {
        public string? AdminAction { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
    }
}
