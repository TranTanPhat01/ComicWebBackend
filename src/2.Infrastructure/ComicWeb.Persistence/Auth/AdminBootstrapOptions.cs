namespace ComicWeb.Persistence.Auth;

public sealed class AdminBootstrapOptions { public const string SectionName = "BootstrapAdmin"; public bool Enabled { get; init; } = true; public string Username { get; init; } = ""; public string Email { get; init; } = ""; public string Password { get; init; } = ""; }
