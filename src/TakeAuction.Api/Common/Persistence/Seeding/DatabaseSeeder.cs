using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Persistence.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        DbContext context,
        SeedOptions options,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return;
        }

        var db = (AppDbContext)context;

        var desired = new[]
        {
            (Email: options.AdminEmail, DisplayName: "Platform Admin", Role: UserRole.Admin),
            (Email: options.SellerEmail, DisplayName: "Demo Seller", Role: UserRole.Seller),
            (Email: options.BidderEmail, DisplayName: "Demo Bidder", Role: UserRole.Bidder)
        };

        var normalizedEmails = desired
            .Select(d => d.Email.Trim().ToLowerInvariant())
            .ToArray();

        var existingEmails = await db.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        var missing = desired
            .Where(d => !existingEmails.Contains(d.Email.Trim().ToLowerInvariant()))
            .Select(d => User.Create(
                d.Email,
                d.DisplayName,
                passwordHasher.Hash(options.DefaultPassword),
                d.Role))
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        await db.Users.AddRangeAsync(missing, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
