using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing;

/// <summary>Composition-owned baseline; preserves persisted names while assemblies are extracted.</summary>
public static class PersistedEventIdentityCatalog
{
    static readonly Lazy<Entry[]> Baseline = new(() =>
    {
        using var stream = typeof(PersistedEventIdentityCatalog).Assembly.GetManifestResourceStream("PersistedEventIdentities.json")
            ?? throw new InvalidOperationException("The persisted identity baseline is missing.");
        return JsonSerializer.Deserialize<Entry[]>(stream) ?? throw new InvalidDataException("Empty identity baseline.");
    });

    /// <summary>Call before opening durable writers or replaying state in a composition root.</summary>
    public static int RegisterProductionClosure(Assembly root, EventWriteIdentityRegistry? writer = null)
    {
        writer ??= EventWriteIdentityRegistry.Default;
        var pending = new Queue<Assembly>();
        pending.Enqueue(root);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var assembly))
        {
            if (!seen.Add(assembly.FullName!)) continue;
            foreach (var dependency in assembly.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("TomasAI.IFM.", StringComparison.Ordinal)))
                pending.Enqueue(Assembly.Load(dependency));
            foreach (var type in assembly.GetTypes().Where(t => typeof(IEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters))
                if (!types.TryAdd(type.FullName!, type)) throw new InvalidOperationException($"Ambiguous event type '{type.FullName}'.");
        }
        var count = 0;
        foreach (var row in Baseline.Value)
        {
            if (!types.TryGetValue(row.currentType, out var type)) continue; // A maintenance root may load only part of the production graph.
            writer.RegisterLegacyIdentity(type, row.EventName, row.EventTypeName);
            count++;
        }
        return count;
    }

    sealed record Entry(string currentType, string sourceAssembly, string EventName, string EventTypeName);
}
