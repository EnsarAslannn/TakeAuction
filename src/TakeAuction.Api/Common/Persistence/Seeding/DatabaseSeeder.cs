using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Auctions;
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

        await SeedUsersAsync(db, options, passwordHasher, cancellationToken);
        await SeedAuctionsAsync(db, options, cancellationToken);
    }

    private static async Task SeedUsersAsync(
        AppDbContext db,
        SeedOptions options,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
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

    private static async Task SeedAuctionsAsync(
        AppDbContext db,
        SeedOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.SeedAuctions)
        {
            return;
        }

        var sellerEmail = options.SellerEmail.Trim().ToLowerInvariant();

        var sellerId = await db.Users
            .Where(u => u.Email == sellerEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (sellerId == Guid.Empty)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await AddMissingShowcaseAuctionsAsync(db, sellerId, now, cancellationToken);
        await ReopenClosedShowcaseAuctionsAsync(db, now, cancellationToken);
    }

    private static async Task AddMissingShowcaseAuctionsAsync(
        AppDbContext db,
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var titles = ShowcaseCatalog.Items.Select(item => item.Title).ToArray();

        var existingTitles = await db.Auctions
            .Where(a => titles.Contains(a.Title))
            .Select(a => a.Title)
            .ToListAsync(cancellationToken);

        var missing = ShowcaseCatalog.Items
            .Where(item => !existingTitles.Contains(item.Title))
            .Select(item => Auction.Create(
                sellerId,
                item.Title,
                item.Description,
                item.StartingPrice,
                item.MinimumBidIncrement,
                now,
                now.AddDays(item.DurationDays),
                now))
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        await db.Auctions.AddRangeAsync(missing, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task ReopenClosedShowcaseAuctionsAsync(
        AppDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var titles = ShowcaseCatalog.Items.Select(item => item.Title).ToArray();

        var closedTitles = await db.Auctions
            .Where(a => titles.Contains(a.Title)
                && a.Status != AuctionStatus.Cancelled
                && a.EndsAtUtc <= now)
            .Select(a => a.Title)
            .ToListAsync(cancellationToken);

        if (closedTitles.Count == 0)
        {
            return;
        }

        foreach (var item in ShowcaseCatalog.Items.Where(item => closedTitles.Contains(item.Title)))
        {
            var endsAtUtc = now.AddDays(item.DurationDays);

            await db.Auctions
                .Where(a => a.Title == item.Title
                    && a.Status != AuctionStatus.Cancelled
                    && a.EndsAtUtc <= now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(a => a.EndsAtUtc, endsAtUtc)
                        .SetProperty(a => a.Status, AuctionStatus.Active),
                    cancellationToken);
        }
    }
}
