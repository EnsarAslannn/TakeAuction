using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("auctions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.SellerId)
            .IsRequired();

        builder.HasIndex(a => a.SellerId);

        builder.Property(a => a.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(a => a.StartingPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.CurrentPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.MinimumBidIncrement)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.StartsAtUtc)
            .IsRequired();

        builder.Property(a => a.EndsAtUtc)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.Property(a => a.Version)
            .IsRowVersion();

        builder.HasIndex(a => new { a.Status, a.EndsAtUtc });

        builder.HasOne<Domain.Users.User>()
            .WithMany()
            .HasForeignKey(a => a.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
