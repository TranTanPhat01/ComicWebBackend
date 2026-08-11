using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComicWeb.Domain.Enums;

namespace ComicWeb.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; private set; } = string.Empty;
        public string NormalizedUsername { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string NormalizedEmail { get; private set; } = string.Empty;
        public UserRole Role { get; private set; } = UserRole.User;
        public bool IsActive { get; private set; } = true;
        public bool MustChangePassword { get; private set; }
        public int FailedLoginAttempts { get; private set; }
        public DateTime? LockoutEndAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }

        private User() { }

        public User(string username, string email, string passwordHash, UserRole role, bool mustChangePassword, DateTime now)
        {
            SetIdentity(username, email);
            PasswordHash = passwordHash;
            Role = role;
            MustChangePassword = mustChangePassword;
            CreatedAt = now;
        }

        public void RecordFailedLogin(DateTime now, int maximumAttempts, TimeSpan lockoutDuration)
        {
            FailedLoginAttempts++;
            if (FailedLoginAttempts >= maximumAttempts) LockoutEndAt = now.Add(lockoutDuration);
            UpdatedAt = now;
        }

        public void RecordSuccessfulLogin(DateTime now)
        {
            FailedLoginAttempts = 0;
            LockoutEndAt = null;
            LastLoginAt = now;
            UpdatedAt = now;
        }

        public bool IsLockedOut(DateTime now) => LockoutEndAt is not null && LockoutEndAt > now;

        public void ChangePassword(string passwordHash, DateTime now)
        {
            PasswordHash = passwordHash;
            MustChangePassword = false;
            UpdatedAt = now;
        }

        private void SetIdentity(string username, string email)
        {
            Username = username.Trim();
            Email = email.Trim();
            NormalizedUsername = Username.ToUpperInvariant();
            NormalizedEmail = Email.ToUpperInvariant();
        }
    }
}
