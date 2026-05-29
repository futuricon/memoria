using System.Reflection;

namespace Memoria.ArchitectureTests.Infrastructure;

internal static class ProjectAssemblies
{
    public static Assembly Users => typeof(Memoria.Users.UsersAssemblyMarker).Assembly;
    public static Assembly UsersContracts => typeof(Memoria.Users.Contracts.UsersContractsAssemblyMarker).Assembly;
    public static Assembly Cards => typeof(Memoria.Cards.CardsAssemblyMarker).Assembly;
    public static Assembly CardsContracts => typeof(Memoria.Cards.Contracts.CardsContractsAssemblyMarker).Assembly;
    public static Assembly Reminders => typeof(Memoria.Reminders.RemindersAssemblyMarker).Assembly;
    public static Assembly RemindersContracts => typeof(Memoria.Reminders.Contracts.RemindersContractsAssemblyMarker).Assembly;
    public static Assembly Reviews => typeof(Memoria.Reviews.ReviewsAssemblyMarker).Assembly;
    public static Assembly ReviewsContracts => typeof(Memoria.Reviews.Contracts.ReviewsContractsAssemblyMarker).Assembly;
    public static Assembly Ai => typeof(Memoria.AI.AiAssemblyMarker).Assembly;
    public static Assembly AiContracts => typeof(Memoria.AI.Contracts.AiContractsAssemblyMarker).Assembly;
    public static Assembly Bot => typeof(Memoria.Bot.BotAssemblyMarker).Assembly;
    public static Assembly Api => typeof(Memoria.Api.ApiAssemblyMarker).Assembly;
    public static Assembly Host => typeof(Memoria.Host.HostAssemblyMarker).Assembly;
    public static Assembly SharedKernel => typeof(Memoria.Shared.Kernel.SharedKernelAssemblyMarker).Assembly;
    public static Assembly SharedInfrastructure => typeof(Memoria.Shared.Infrastructure.SharedInfrastructureAssemblyMarker).Assembly;

    public static IReadOnlyList<Assembly> AllInternalModules =>
        new[] { Users, Cards, Reminders, Reviews, Ai };

    public static IReadOnlyList<Assembly> AllContracts =>
        new[] { UsersContracts, CardsContracts, RemindersContracts, ReviewsContracts, AiContracts };
}
