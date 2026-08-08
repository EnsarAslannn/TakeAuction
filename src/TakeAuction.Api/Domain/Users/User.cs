namespace TakeAuction.Api.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }
    public uint Version { get; private set; }

    private User() { }

    public static User Create(string email, string displayName, string passwordHash, UserRole role)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void RecordLogin() => LastLoginAtUtc = DateTimeOffset.UtcNow;

    public void ChangePassword(string newPasswordHash) => PasswordHash = newPasswordHash;

    public void Deactivate() => IsActive = false;
}
