using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Memoria.Cards;
using Memoria.Reminders;
using Memoria.Reviews;
using Memoria.Users;
using Memoria.Users.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Memoria is starting up");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);
    });

    builder.Services
        .AddUsersModule(builder.Configuration)
        .AddCardsModule(builder.Configuration)
        .AddRemindersModule(builder.Configuration)
        .AddReviewsModule(builder.Configuration);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.MigrateUsersModuleAsync();
    }

    app.UseSerilogRequestLogging();
    app.MapGet("/", () => "Memoria 0.1.0");

    Log.Information("Memoria started, listening for requests");
    await app.RunAsync();

    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Memoria terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}