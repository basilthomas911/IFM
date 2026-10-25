using System.Reflection;
using System.Text.Json;
static class PublicPortQualification
{
    public static void Run(IReadOnlyList<Type> types, string destination)
    {
        var findings = new SortedSet<string>(StringComparer.Ordinal);
        var checkedTypes = new HashSet<Type>();
        var roots = types.Where(type => type.IsVisible && type.Assembly.GetName().Name!.StartsWith("TomasAI.IFM.Domain.", StringComparison.Ordinal)).ToArray();
        foreach (var root in roots)
        {
            Check(root.BaseType, root.FullName + " base");
            foreach (var item in root.GetInterfaces()) Check(item, root.FullName + " interface");
            foreach (var constructor in root.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(constructor => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly))
                foreach (var parameter in constructor.GetParameters()) Check(parameter.ParameterType, root.FullName + " constructor");
            Surface(root);
        }
        void Check(Type? type, string origin)
        {
            if (type is null || type.IsGenericParameter) return;
            if (type.HasElementType) { Check(type.GetElementType(), origin); return; }
            foreach (var argument in type.GetGenericArguments()) Check(argument, origin);
            var owner = type.Assembly.GetName().Name!;
            if (owner.StartsWith("TomasAI.IFM.Framework.", StringComparison.Ordinal)
                || new[] { "NATS.", "Npgsql", "Cassandra", "Dapper", "AWSSDK.", "Hazelcast", "StackExchange.Redis" }.Any(prefix => owner.StartsWith(prefix, StringComparison.Ordinal)))
            { findings.Add(origin + " exposes " + type.FullName + " [" + owner + "]"); return; }
            if (owner.StartsWith("TomasAI.IFM.Application.", StringComparison.Ordinal) || owner == "TomasAI.IFM.Shared")
            {
                if (!checkedTypes.Add(type)) return;
                // Follow contract inheritance and callable instance signatures; implementation constructors
                // are checked when Domain actually binds them by the separate compiled-reference gate.
                foreach (var port in type.GetInterfaces()) Check(port, type.FullName + " interface");
                Surface(type);
            }
        }
        void Surface(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags).Where(method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly))
            {
                var origin = type.FullName + "." + method.Name;
                Check(method.ReturnType, origin);
                foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType, origin);
                foreach (var parameter in method.GetGenericArguments().Where(parameter => parameter.IsGenericParameter))
                    foreach (var constraint in parameter.GetGenericParameterConstraints()) Check(constraint, origin);
            }
            foreach (var field in type.GetFields(flags).Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly)) Check(field.FieldType, type.FullName + "." + field.Name);
        }
        File.WriteAllText(destination, JsonSerializer.Serialize(new { domainTypes = roots.Length, referencedContractTypes = checkedTypes.Count, violations = findings }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Public port audit: {roots.Length} Domain types, {checkedTypes.Count} reachable contract types, {findings.Count} leaks.");
        if (findings.Count > 0) throw new InvalidOperationException("Public port audit failed; see " + destination);
    }
}
