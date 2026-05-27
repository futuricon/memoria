using System.Reflection;

using FluentAssertions;

using MediatR;

using Memoria.ArchitectureTests.Infrastructure;

namespace Memoria.ArchitectureTests;

/// <summary>
/// Правило 6: каждый concrete-класс, имя которого заканчивается на "Handler" в
/// internal-модулях, реализует <see cref="IRequestHandler{TRequest, TResponse}"/>
/// или <see cref="INotificationHandler{TNotification}"/>. NetArchTest не умеет
/// проверять open-generic интерфейсы, поэтому делаем через reflection.
/// </summary>
public sealed class HandlerConformanceTests
{
    public static IEnumerable<object[]> ModuleAssemblies()
    {
        foreach (var asm in ProjectAssemblies.AllInternalModules)
        {
            yield return new object[] { asm, asm.GetName().Name! };
        }
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void AllTypesEndingInHandlerImplementMediatRInterface(Assembly module, string label)
    {
        var nonConforming = module.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Where(t => !ImplementsMediatRHandler(t))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        nonConforming.Should().BeEmpty(
            $"{label} has *Handler classes that don't implement MediatR handler interfaces: " +
            $"{string.Join(", ", nonConforming)}");
    }

    private static bool ImplementsMediatRHandler(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType
            && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                || i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)));
}
