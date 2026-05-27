using System.Reflection;

using FluentAssertions;

using Memoria.ArchitectureTests.Infrastructure;

using NetArchTest.Rules;

namespace Memoria.ArchitectureTests;

/// <summary>
/// Правило 3: <c>Memoria.*.Contracts</c> зависят только от <c>Memoria.Shared.Kernel</c>
/// и MediatR-абстракций. Никаких EF, Hangfire, Telegram.Bot, ASP.NET, FluentValidation,
/// Shared.Infrastructure или внутренностей других модулей.
/// </summary>
public sealed class ContractsPurityTests
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Hangfire",
        "Telegram.Bot",
        "Microsoft.AspNetCore",
        "FluentValidation",
        "Microsoft.Extensions.Hosting",
        "Memoria.Shared.Infrastructure",
        "Memoria.Bot",
        "Memoria.Api",
        "Memoria.Host",
        // Внутренности других модулей (см. ModuleIsolationTests на полный список).
        "Memoria.Users.Domain", "Memoria.Users.Persistence",
        "Memoria.Users.Features", "Memoria.Users.Services", "Memoria.Users.Options",
        "Memoria.Cards.Domain", "Memoria.Cards.Persistence",
        "Memoria.Cards.Features", "Memoria.Cards.Services", "Memoria.Cards.Jobs",
        "Memoria.Cards.Options",
        "Memoria.Reminders.Domain", "Memoria.Reminders.Persistence",
        "Memoria.Reminders.Features", "Memoria.Reminders.Services", "Memoria.Reminders.Jobs",
        "Memoria.Reminders.Options",
        "Memoria.Reviews.Domain", "Memoria.Reviews.Persistence",
        "Memoria.Reviews.Features", "Memoria.Reviews.Services",
    ];

    public static IEnumerable<object[]> ContractsAssemblies()
    {
        yield return new object[] { ProjectAssemblies.UsersContracts, "Users.Contracts" };
        yield return new object[] { ProjectAssemblies.CardsContracts, "Cards.Contracts" };
        yield return new object[] { ProjectAssemblies.RemindersContracts, "Reminders.Contracts" };
        yield return new object[] { ProjectAssemblies.ReviewsContracts, "Reviews.Contracts" };
    }

    [Theory]
    [MemberData(nameof(ContractsAssemblies))]
    public void ContractsProjectsHaveOnlyAllowedDependencies(Assembly contracts, string label)
    {
        var result = Types.InAssembly(contracts)
            .Should()
            .NotHaveDependencyOnAny(ForbiddenNamespaces)
            .GetResult();

        var failing = result.FailingTypeNames ?? Array.Empty<string>();
        result.IsSuccessful.Should().BeTrue(
            $"{label} must stay framework-agnostic. Offending types: {string.Join(", ", failing)}");
    }
}
