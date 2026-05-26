using System.Globalization;

using FluentValidation;

using Hangfire;

using MediatR;

using Serilog;

using Memoria.Cards;
using Memoria.Reminders;
using Memoria.Reviews;
using Memoria.Shared.Infrastructure.Behaviors;
using Memoria.Users;

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

    builder.Services.AddSingleton(TimeProvider.System);

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(Memoria.Users.DependencyInjection).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Memoria.Cards.DependencyInjection).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Memoria.Reminders.DependencyInjection).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Memoria.Reviews.DependencyInjection).Assembly);
    });

    builder.Services.AddValidatorsFromAssembly(
        typeof(Memoria.Users.DependencyInjection).Assembly);

    builder.Services.AddTransient(
        typeof(IPipelineBehavior<,>),
        typeof(ValidationBehavior<,>));

    builder.Services
        .AddUsersModule(builder.Configuration)
        .AddCardsModule(builder.Configuration)
        .AddRemindersModule(builder.Configuration)
        .AddReviewsModule(builder.Configuration);

    string[] hangfireQueues = ["default", "reminders"];
    builder.Services.AddHangfireServer(opts =>
    {
        opts.WorkerCount = 4;
        opts.Queues = hangfireQueues;
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.MigrateUsersModuleAsync();
        await scope.ServiceProvider.MigrateCardsModuleAsync();
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
