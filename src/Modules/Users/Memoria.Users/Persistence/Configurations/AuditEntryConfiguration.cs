using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Users.Domain;

namespace Memoria.Users.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActorUserId).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Subject).HasMaxLength(256).IsRequired();
        builder.Property(a => a.MetadataJson).HasColumnType("jsonb");
        builder.Property(a => a.OccurredAt).IsRequired();

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
    }
}
