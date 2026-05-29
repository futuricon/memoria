using System.Reflection;

using FluentAssertions;

using Memoria.ArchitectureTests.Infrastructure;
using Memoria.Reminders.Contracts.Abstractions;

using NetArchTest.Rules;

namespace Memoria.ArchitectureTests;

/// <summary>
/// Правила 7, 8 + addendum: scope внешних библиотек и реализаций портов.
/// <list type="bullet">
///   <item>Telegram.Bot — только в <c>Memoria.Bot</c> (+ транзитивно в Host).</item>
///   <item>Hangfire — в <c>Memoria.Reminders</c>, <c>Memoria.Cards</c> (purge-job)
///     и presentation-слоях (Api для dashboard, Host для wiring). Никаких
///     Hangfire-зависимостей в Users / Reviews / Contracts.</item>
///   <item>Реализации <see cref="IReminderNotificationSender"/> допустимы только
///     в Bot / Api / Host.</item>
/// </list>
/// </summary>
public sealed class ExternalLibraryScopeTests
{
    private static readonly Assembly[] TelegramForbiddenIn =
    [
        ProjectAssemblies.Users,
        ProjectAssemblies.UsersContracts,
        ProjectAssemblies.Cards,
        ProjectAssemblies.CardsContracts,
        ProjectAssemblies.Reminders,
        ProjectAssemblies.RemindersContracts,
        ProjectAssemblies.Reviews,
        ProjectAssemblies.ReviewsContracts,
        ProjectAssemblies.Ai,
        ProjectAssemblies.AiContracts,
        ProjectAssemblies.Api,
        ProjectAssemblies.SharedKernel,
        ProjectAssemblies.SharedInfrastructure,
    ];

    private static readonly Assembly[] HangfireForbiddenIn =
    [
        ProjectAssemblies.Users,
        ProjectAssemblies.UsersContracts,
        ProjectAssemblies.CardsContracts,
        ProjectAssemblies.RemindersContracts,
        ProjectAssemblies.Reviews,
        ProjectAssemblies.ReviewsContracts,
        ProjectAssemblies.Ai,
        ProjectAssemblies.AiContracts,
        ProjectAssemblies.Bot,
        ProjectAssemblies.SharedKernel,
        ProjectAssemblies.SharedInfrastructure,
    ];

    private static readonly Assembly[] PortImplAllowedIn =
    [
        ProjectAssemblies.Bot, ProjectAssemblies.Api, ProjectAssemblies.Host,
    ];

    [Fact]
    public void TelegramBotTypesAreNotUsedOutsideBotAndHost()
    {
        foreach (var asm in TelegramForbiddenIn)
        {
            var result = Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOn("Telegram.Bot")
                .GetResult();

            var failing = result.FailingTypeNames ?? Array.Empty<string>();
            result.IsSuccessful.Should().BeTrue(
                $"{asm.GetName().Name} must not reference Telegram.Bot. Offending: {string.Join(", ", failing)}");
        }
    }

    [Fact]
    public void HangfireTypesAreNotUsedOutsideAllowedAssemblies()
    {
        foreach (var asm in HangfireForbiddenIn)
        {
            var result = Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOn("Hangfire")
                .GetResult();

            var failing = result.FailingTypeNames ?? Array.Empty<string>();
            result.IsSuccessful.Should().BeTrue(
                $"{asm.GetName().Name} must not reference Hangfire. Offending: {string.Join(", ", failing)}");
        }
    }

    [Fact]
    public void ReminderNotificationSenderImplementationsLiveOnlyInBotApiHost()
    {
        var sender = typeof(IReminderNotificationSender);
        var allInspected = ProjectAssemblies.AllInternalModules
            .Concat(ProjectAssemblies.AllContracts);

        foreach (var asm in allInspected)
        {
            if (PortImplAllowedIn.Contains(asm)) continue;

            var implementers = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && sender.IsAssignableFrom(t))
                .Select(t => t.FullName ?? t.Name)
                .ToList();

            implementers.Should().BeEmpty(
                $"{asm.GetName().Name} must not implement {sender.Name}. " +
                $"Found: {string.Join(", ", implementers)}");
        }
    }
}
