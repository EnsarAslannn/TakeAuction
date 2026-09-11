using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Domain.Watchlist;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
