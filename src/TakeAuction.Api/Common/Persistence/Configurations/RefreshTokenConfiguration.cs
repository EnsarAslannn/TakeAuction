using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.UserId)
            .IsRequired();

        builder.Property(token => token.FamilyId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        // Every refresh is a lookup by hash, and the uniqueness doubles as a guard against
        // two rows ever answering to the same presented token.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        // Reuse detection burns a whole family at once, and the purge sweep scans by expiry.
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.ExpiresAtUtc);

        builder.Property(token => token.CreatedAtUtc)
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
