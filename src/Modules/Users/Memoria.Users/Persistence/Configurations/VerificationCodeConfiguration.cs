using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Users.Domain;

namespace Memoria.Users.Persistence.Configurations;

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("verification_codes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId);
        builder.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.TargetIdentifier).HasMaxLength(320).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(256).IsRequired();
        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.ConsumedAt);
        builder.Property(c => c.AttemptCount).IsRequired();

        builder.HasIndex(c => new { c.TargetIdentifier, c.Purpose });
        builder.HasIndex(c => c.ExpiresAt);
    }
}