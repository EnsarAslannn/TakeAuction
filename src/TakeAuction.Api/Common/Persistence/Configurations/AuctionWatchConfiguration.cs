using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Domain.Watchlist;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class AuctionWatchConfiguration : IEntityTypeConfiguration<AuctionWatch>
{
    public void Configure(EntityTypeBuilder<AuctionWatch> builder)
    {
        builder.ToTable("auction_watches");

        builder.HasKey(watch => new { watch.UserId, watch.AuctionId });

        builder.Property(watch => watch.CreatedAtUtc)
            .IsRequired();

        builder.Property(watch => watch.ClosingSoonNotifiedAtUtc);

        builder.HasIndex(watch => watch.AuctionId)
            .HasFilter("\"ClosingSoonNotifiedAtUtc\" IS NULL")
            .HasDatabaseName("IX_auction_watches_awaiting_reminder");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(watch => watch.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Auction>()
            .WithMany()
            .HasForeignKey(watch => watch.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
