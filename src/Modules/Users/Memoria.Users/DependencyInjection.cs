using System.Net.Http.Headers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Users.Contracts.Abstractions;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

namespace Memoria.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'Postgres' is not configured.");

        services.AddDbContext<UsersDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(
                        tableName: "__ef_migrations_history",
                        schema: UsersDbContext.SchemaName))
                .UseSnakeCaseNamingConvention());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<VerificationCodeOptions>()
            .Bind(configuration.GetSection(VerificationCodeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        services.AddOptions<EmailOptions>().Bind(emailSection);
        var emailOptions = emailSection.Get<EmailOptions>() ?? new EmailOptions();

        services.AddSingleton<VerificationCodeService>();
        services.AddSingleton<JwtTokenIssuer>();

        if (!string.IsNullOrWhiteSpace(emailOptions.ApiKey))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>(ConfigureResend);
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }

    private static void ConfigureResend(IServiceProvider sp, HttpClient http)
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        http.BaseAddress = new Uri(ResendEmailSender.DefaultBaseUrl);
        http.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    public static async Task MigrateUsersModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var db = services.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
    }
}
