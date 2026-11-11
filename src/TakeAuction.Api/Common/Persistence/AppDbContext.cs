using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Domain.Watchlist;
using TakeAuction.Api.Features.Media;

namespace TakeAuction.Api.Common.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Auction> Auctions => Set<Auction>();

    public DbSet<Bid> Bids => Set<Bid>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuctionWatch> AuctionWatches => Set<AuctionWatch>();
    public DbSet<AuctionImage> AuctionImages => Set<AuctionImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
