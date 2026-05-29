using Memoria.Reminders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.Reminders.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ScheduledAt)
            .IsRequired();
        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(r => r.UserId)
            .IsRequired();
        builder.Property(r => r.CardId)
            .IsRequired();
        builder.Property(r => r.StageNumber)
            .IsRequired();
        builder.Property(r => r.HangfireJobId)
            .HasMaxLength(64);
        builder.Property(r => r.MessageId);
        builder.Property(r => r.ConfirmedAt);
        builder.Property(r => r.SentAt);

        // Adaptive scheduling means a card accumulates many reminders over time
        // with repeating stage numbers, so this index is NO LONGER unique.
        builder.HasIndex(r => new { r.CardId, r.StageNumber });
        builder.HasIndex(r => new { r.CardId, r.Status }); // "does this card have a pending reminder?"
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.Status, r.ScheduledAt }); // SendReminderJob: "what's pending and due"
    }
}