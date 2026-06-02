using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.AI.Domain;

namespace Memoria.AI.Persistence.Configurations;

internal sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ai_usage");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserId).IsRequired();
        builder.Property(u => u.Operation)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(u => u.Model)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(u => u.InputTokens).IsRequired();
        builder.Property(u => u.OutputTokens).IsRequired();
        builder.Property(u => u.IsFailure).IsRequired();
        builder.Property(u => u.OccurredAt).IsRequired();

        // Hot read paths for the admin dashboard:
        //   * per-user lifetime totals (joined with the users page),
        //   * global trend by occurred_at,
        //   * top spenders ordered by sum(tokens) per user_id.
        builder.HasIndex(u => new { u.UserId, u.OccurredAt });
        builder.HasIndex(u => u.OccurredAt);
    }
}
