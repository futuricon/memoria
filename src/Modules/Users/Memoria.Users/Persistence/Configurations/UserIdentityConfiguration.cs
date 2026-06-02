using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Users.Domain;

namespace Memoria.Users.Persistence.Configurations;

internal sealed class UserIdentityConfiguration : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_identities");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UserId).IsRequired();
        builder.Property(i => i.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.ExternalId).HasMaxLength(256).IsRequired();
        builder.Property(i => i.LinkedAt).IsRequired();
        builder.Property(i => i.ExternalDisplayName).HasMaxLength(64);

        builder.HasIndex(i => new { i.Provider, i.ExternalId }).IsUnique();
        builder.HasIndex(i => i.UserId);
    }
}