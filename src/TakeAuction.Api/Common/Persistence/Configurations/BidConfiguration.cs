using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("bids");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.AuctionId)
            .IsRequired();

        builder.Property(b => b.BidderId)
            .IsRequired();

        builder.Property(b => b.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.PlacedAtUtc)
            .IsRequired();

        builder.HasIndex(b => new { b.AuctionId, b.Amount })
            .IsDescending(false, true);

        builder.HasIndex(b => b.BidderId);

        builder.HasOne<Auction>()
            .WithMany()
            .HasForeignKey(b => b.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Users.User>()
            .WithMany()
            .HasForeignKey(b => b.BidderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
