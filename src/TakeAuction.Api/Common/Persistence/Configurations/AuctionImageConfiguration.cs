using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Features.Media;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class AuctionImageConfiguration : IEntityTypeConfiguration<AuctionImage>
{
    public void Configure(EntityTypeBuilder<AuctionImage> builder)
    {
        builder.ToTable("auction_images");
        builder.HasKey(image => image.FileName);
        builder.Property(image => image.FileName).HasMaxLength(37);
        builder.Property(image => image.ContentType).HasMaxLength(16);
        builder.HasIndex(image => image.CreatedAtUtc);
    }
}
