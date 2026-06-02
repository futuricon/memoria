using Microsoft.EntityFrameworkCore;

using Memoria.AI.Domain;
using Memoria.Shared.Infrastructure.Persistence;

namespace Memoria.AI.Persistence;

internal sealed class AiDbContext : DbContext
{
    public const string SchemaName = "ai";

    public AiDbContext(DbContextOptions<AiDbContext> options) : base(options)
    {
    }

    internal DbSet<AiUsage> Usage => Set<AiUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
