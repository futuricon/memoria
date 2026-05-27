using FluentAssertions;

using Memoria.ArchitectureTests.Infrastructure;

using NetArchTest.Rules;

namespace Memoria.ArchitectureTests;

/// <summary>
/// Правила 1, 2, 5 из brief: internal-код модуля X не зависит от internal-кода
/// модуля Y. Зависимости разрешены только на свой Contracts + чужие Contracts +
/// Shared. Memoria.Bot и Memoria.Api никогда не ссылаются на internal модулей.
/// <para>
/// NetArchTest сверяет namespace по prefix, поэтому ловим именно internal-под-namespace-ы
/// (Domain, Persistence, Features, Services, Options, Jobs, Adapters, Callbacks,
/// Commands, Conversations, Routing) — они отсутствуют в Contracts-проектах.
/// </para>
/// </summary>
public sealed class ModuleIsolationTests
{
    private static readonly string[] UsersInternalNamespaces =
    [
        "Memoria.Users.Domain", "Memoria.Users.Persistence",
        "Memoria.Users.Features", "Memoria.Users.Services", "Memoria.Users.Options",
    ];

    private static readonly string[] CardsInternalNamespaces =
    [
        "Memoria.Cards.Domain", "Memoria.Cards.Persistence",
        "Memoria.Cards.Features", "Memoria.Cards.Services", "Memoria.Cards.Jobs",
        "Memoria.Cards.Options",
    ];

    private static readonly string[] RemindersInternalNamespaces =
    [
        "Memoria.Reminders.Domain", "Memoria.Reminders.Persistence",
        "Memoria.Reminders.Features", "Memoria.Reminders.Services", "Memoria.Reminders.Jobs",
        "Memoria.Reminders.Options",
    ];

    private static readonly string[] ReviewsInternalNamespaces =
    [
        "Memoria.Reviews.Domain", "Memoria.Reviews.Persistence",
        "Memoria.Reviews.Features", "Memoria.Reviews.Services",
    ];

    [Fact]
    public void UsersInternalDoesNotDependOnOtherModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Users,
            CardsInternalNamespaces
                .Concat(RemindersInternalNamespaces)
                .Concat(ReviewsInternalNamespaces)
                .ToArray());
    }

    [Fact]
    public void CardsInternalDoesNotDependOnOtherModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Cards,
            UsersInternalNamespaces
                .Concat(RemindersInternalNamespaces)
                .Concat(ReviewsInternalNamespaces)
                .ToArray());
    }

    [Fact]
    public void RemindersInternalDoesNotDependOnOtherModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Reminders,
            UsersInternalNamespaces
                .Concat(CardsInternalNamespaces)
                .Concat(ReviewsInternalNamespaces)
                .ToArray());
    }

    [Fact]
    public void ReviewsInternalDoesNotDependOnOtherModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Reviews,
            UsersInternalNamespaces
                .Concat(CardsInternalNamespaces)
                .Concat(RemindersInternalNamespaces)
                .ToArray());
    }

    [Fact]
    public void BotDoesNotDependOnAnyModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Bot,
            UsersInternalNamespaces
                .Concat(CardsInternalNamespaces)
                .Concat(RemindersInternalNamespaces)
                .Concat(ReviewsInternalNamespaces)
                .ToArray());
    }

    [Fact]
    public void ApiDoesNotDependOnAnyModuleInternals()
    {
        AssertNoDependencies(
            ProjectAssemblies.Api,
            UsersInternalNamespaces
                .Concat(CardsInternalNamespaces)
                .Concat(RemindersInternalNamespaces)
                .Concat(ReviewsInternalNamespaces)
                .ToArray());
    }

    private static void AssertNoDependencies(System.Reflection.Assembly assembly, string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        var failing = result.FailingTypeNames ?? Array.Empty<string>();
        result.IsSuccessful.Should().BeTrue(
            $"{assembly.GetName().Name} must not depend on {{ {string.Join(", ", forbidden)} }}. " +
            $"Offending types: {string.Join(", ", failing)}");
    }
}
