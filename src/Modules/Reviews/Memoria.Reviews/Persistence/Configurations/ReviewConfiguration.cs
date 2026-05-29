using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Memoria.Reviews.Contracts;
using Memoria.Reviews.Domain;

namespace Memoria.Reviews.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CardId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.ReminderId);
        builder.Property(r => r.Rating)
            .HasConversion<string>()
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(r => r.CardTitleSnapshot)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(r => r.ReviewedAt).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.AnswerText).HasMaxLength(ReviewConstraints.MaxAnswerLength);
        builder.Property(r => r.AiScore);
        builder.Property(r => r.AiFeedback).HasMaxLength(2000);
        builder.Property(r => r.AutoGraded).IsRequired();

        builder.HasIndex(r => r.CardId);
        builder.HasIndex(r => new { r.UserId, r.ReviewedAt });
        builder.HasIndex(r => r.ReminderId);
    }
}
