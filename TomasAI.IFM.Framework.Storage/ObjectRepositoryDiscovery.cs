using System.Reflection;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>Discovers public container-owned repositories, not private helpers owned by a store.</summary>
public static class ObjectRepositoryDiscovery
{
    public static Type[] Discover(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return assemblies.Where(static assembly => !assembly.IsDynamic)
            .Distinct()
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .Where(static type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && type.GetInterfaces().Any(static contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IObjectRepository<>)))
            .Distinct()
            .ToArray();
    }
}
