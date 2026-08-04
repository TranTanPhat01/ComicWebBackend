using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ComicWeb.Application.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Contexts
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Story> Stories => Set<Story>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Genre> Genres => Set<Genre>();
        public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tự động quét và áp dụng tất cả các file cấu hình Fluent API trong cùng Assembly này
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
