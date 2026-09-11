using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(notification => notification.AuctionTitle)
            .HasMaxLength(Notification.MaxAuctionTitleLength)
            .IsRequired();

        builder.Property(notification => notification.Amount)
            .HasPrecision(18, 2);

        builder.Property(notification => notification.AuctionEndsAtUtc)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc);

        builder.HasIndex(notification => new { notification.UserId, notification.Kind, notification.AuctionId })
            .IsUnique()
            .HasDatabaseName("UX_notifications_user_kind_auction");

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAtUtc })
            .IsDescending(false, true);

        builder.HasIndex(notification => notification.UserId)
            .HasFilter("\"ReadAtUtc\" IS NULL")
            .HasDatabaseName("IX_notifications_unread");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Auction>()
            .WithMany()
            .HasForeignKey(notification => notification.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
