using ComicWeb.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Common.Interface
{
    public interface IApplicationDbContext
    {
        DbSet<Story> Stories { get; }
        DbSet<Chapter> Chapters { get; }
        DbSet<SystemLog> SystemLogs { get; }
        DbSet<UserNotification> UserNotifications { get; }

        DbSet<User> Users { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
