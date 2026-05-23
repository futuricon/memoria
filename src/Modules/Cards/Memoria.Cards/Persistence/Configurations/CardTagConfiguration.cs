using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Cards.Domain;

namespace Memoria.Cards.Persistence.Configurations;

internal sealed class CardTagConfiguration : IEntityTypeConfiguration<CardTag>
{
    public void Configure(EntityTypeBuilder<CardTag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("card_tags");
        builder.HasKey(ct => new { ct.CardId, ct.TagId });

        builder.HasIndex(ct => ct.TagId);
    }
}
