using FluentAssertions;

using Memoria.ArchitectureTests.Infrastructure;

namespace Memoria.ArchitectureTests;

/// <summary>
/// Правило 4: Host — единственный composition root, который ссылается на
/// internal-сборки всех модулей. Bot/Api не должны иметь project-references
/// на internal модули (только на Contracts).
/// </summary>
public sealed class HostCompositionTests
{
    [Fact]
    public void HostReferencesAllInternalModules()
    {
        var hostRefs = ProjectAssemblies.Host
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var module in ProjectAssemblies.AllInternalModules)
        {
            var moduleName = module.GetName().Name!;
            hostRefs.Should().Contain(moduleName,
                because: $"Host must reference {moduleName} for composition");
        }
    }

    [Fact]
    public void BotDoesNotReferenceModuleInternals()
    {
        AssertNoInternalReferences(ProjectAssemblies.Bot);
    }

    [Fact]
    public void ApiDoesNotReferenceModuleInternals()
    {
        AssertNoInternalReferences(ProjectAssemblies.Api);
    }

    private static void AssertNoInternalReferences(System.Reflection.Assembly assembly)
    {
        var refs = assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var internalModule in ProjectAssemblies.AllInternalModules)
        {
            var name = internalModule.GetName().Name!;
            refs.Should().NotContain(name,
                because: $"{assembly.GetName().Name} must not reference {name} (use Contracts instead)");
        }
    }
}
