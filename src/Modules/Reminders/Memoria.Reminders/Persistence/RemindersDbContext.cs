using Memoria.Reminders.Domain;
using Memoria.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Persistence;

internal sealed class RemindersDbContext : DbContext
{
    public const string SchemaName = "reminders";

    public RemindersDbContext(DbContextOptions<RemindersDbContext> options) : base(options) { }

    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RemindersDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}