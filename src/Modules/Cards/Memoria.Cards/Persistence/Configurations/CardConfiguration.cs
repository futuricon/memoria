using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Cards.Contracts;
using Memoria.Cards.Domain;

namespace Memoria.Cards.Persistence.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cards");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(CardConstraints.MaxTitleLength).IsRequired();
        builder.Property(c => c.Body).HasMaxLength(CardConstraints.MaxBodyLength).IsRequired();
        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.DeletedAt);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.DeletedAt);
        builder.HasIndex(c => new { c.UserId, c.CreatedAt });

        builder.HasMany(c => c.CardTags)
            .WithOne()
            .HasForeignKey(ct => ct.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
