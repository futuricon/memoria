using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Users.Domain;

namespace Memoria.Users.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(u => u.QuietHoursStart);
        builder.Property(u => u.QuietHoursEnd);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.DeletedAt);
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(Contracts.Dtos.Role.User);
        builder.Property(u => u.LastSeenAt);
        builder.Property(u => u.IsBlocked).IsRequired().HasDefaultValue(false);

        builder.HasIndex(u => u.Email).IsUnique().HasFilter("email IS NOT NULL");
        builder.HasIndex(u => u.DeletedAt);

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}