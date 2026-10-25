using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Explicit read aliases for stable event IDs and historical CLR identities.
/// Registration is additive and atomic; an identity can never be rebound to another type.
/// Writers continue to use their existing storage identities.
/// </summary>
public sealed class EventTypeRegistry : IEventTypeResolver
{
    public static EventTypeRegistry Default { get; } = new(loadCompatibilityCatalog: true);
    readonly object gate = new();
    ImmutableDictionary<string, Type> aliases = ImmutableDictionary.Create<string, Type>(StringComparer.Ordinal);
    readonly ImmutableDictionary<string, string> compatibilityTargets = ImmutableDictionary<string, string>.Empty;
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> resolvedTargets = new(StringComparer.Ordinal);
    static readonly Regex StableName = new(@"\A[a-z][a-z0-9]*(?:[.-][a-z0-9]+)+/v[1-9][0-9]*\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public EventTypeRegistry() { }

    /// <summary>Creates an isolated reader with the deployed legacy-to-current metadata, without host initialization.</summary>
    public static EventTypeRegistry CreateCompatibilityRegistry() => new(loadCompatibilityCatalog: true);

    EventTypeRegistry(bool loadCompatibilityCatalog)
    {
        if (loadCompatibilityCatalog) compatibilityTargets = PersistedIdentityMetadata.ReadTargets();
    }

    /// <summary>Registers a stable ID (for example trade.position-changed/v1) and exact historical names.</summary>
    public void Register<TEvent>(string stableIdentity, params string[] historicalIdentities) where TEvent : IEvent
        => Register(typeof(TEvent), stableIdentity, historicalIdentities);

    public void Register(Type eventType, string stableIdentity, params string[] historicalIdentities)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        RequireEventType(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(stableIdentity);
        if (!StableName.IsMatch(stableIdentity) || stableIdentity.Length > 512)
            throw new ArgumentException("Stable event identities must use lowercase dotted/hyphenated names ending in /vN.", nameof(stableIdentity));
        ArgumentNullException.ThrowIfNull(historicalIdentities);
        RegisterIdentities(eventType, new[] { stableIdentity }.Concat(historicalIdentities).Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Registers an exact persisted CLR spelling without introducing a new stable event ID.</summary>
    public void RegisterHistoricalIdentity(Type eventType, string identity)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        RequireEventType(eventType);
        RegisterIdentities(eventType, [identity]);
    }

    void RegisterIdentities(Type eventType, string[] names)
    {
        foreach (var name in names)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            // A resolvable CLR identity cannot silently be redirected to a different event class.
            var existing = ResolveClr(name);
            if (existing is not null && existing != eventType)
                throw new InvalidOperationException($"Event identity '{name}' already identifies '{existing.FullName}'.");
        }
        lock (gate)
        {
            var current = aliases;
            foreach (var name in names)
            {
                if (current.TryGetValue(name, out var existing) && existing != eventType)
                    throw new InvalidOperationException($"Event identity '{name}' is already registered for '{existing.FullName}'.");
                if (TryCompatibilityTarget(name, out var target) && ResolveClr(target) is { } mapped && mapped != eventType)
                    throw new InvalidOperationException($"Event identity '{name}' is frozen for '{mapped.FullName}'.");
            }
            // Publish the entire batch only after every alias has passed validation.
            Volatile.Write(ref aliases, current.SetItems(names.Select(name => new KeyValuePair<string, Type>(name, eventType))));
        }
    }

    public Type? Resolve(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var snapshot = Volatile.Read(ref aliases);
        var type = snapshot.GetValueOrDefault(identity);
        if (type is null && !resolvedTargets.TryGetValue(identity, out type))
        {
            type = ResolveClr(identity);
            if (TryCompatibilityTarget(identity, out var target))
            {
                var relocated = ResolveClr(target);
                if (type is not null && relocated is not null && type != relocated)
                    throw new InvalidOperationException($"Event identity '{identity}' resolves to conflicting old and relocated classes.");
                type ??= relocated;
            }
            if (type is not null) { RequireEventType(type); resolvedTargets.TryAdd(identity, type); }
        }
        if (type is not null) RequireEventType(type);
        return type;
    }

    /// <summary>Durable delivery and direct codec reads must fail explicitly for unavailable identities.</summary>
    public Type ResolveRequired(string identity)
        => RequireResolved(Resolve(identity), identity);

    /// <summary>Known persisted spellings for typed snapshot/range selection; current writes remain unchanged.</summary>
    public string[] GetReadIdentities(Type eventType)
    {
        RequireEventType(eventType);
        return Volatile.Read(ref aliases).Where(pair => pair.Value == eventType).Select(pair => pair.Key)
            .Concat(compatibilityTargets.Where(pair => pair.Value == $"{eventType.FullName}, {eventType.Assembly.GetName().Name}").Select(pair => pair.Key))
            .Append(eventType.AssemblyQualifiedName!).Distinct(StringComparer.Ordinal).ToArray();
    }

    bool TryCompatibilityTarget(string identity, out string target)
    {
        if (compatibilityTargets.TryGetValue(identity, out target!)) return true;
        // Version-independent lookup is restricted to an explicitly inventoried
        // full CLR class name and unsigned assembly, never a guessed short name.
        var comma = identity.IndexOf(',');
        if (comma < 0 || identity.Contains('[') || identity.Contains('/')) return false;
        try
        {
            var assembly = new System.Reflection.AssemblyName(identity[(comma + 1)..]);
            if (assembly.GetPublicKeyToken() is { Length: > 0 } || !string.IsNullOrEmpty(assembly.CultureName)) return false;
            return compatibilityTargets.TryGetValue(identity[..comma] + ", " + assembly.Name, out target!);
        }
        catch (ArgumentException) { return false; }
        catch (System.IO.FileLoadException) { return false; }
    }

    internal static Type RequireResolved(Type? type, string identity)
    {
        if (type is not null) return type;
        // Preserve the original CLR reader's missing-assembly/type exception
        // classification. Unknown stable IDs are explicit registry failures.
        if (!identity.Contains('/', StringComparison.Ordinal))
            return Type.GetType(identity, throwOnError: true, ignoreCase: true)!;
        throw new TypeLoadException($"Persisted event identity '{identity}' is not registered or available.");
    }

    static Type? ResolveClr(string identity)
    {
        // Stable IDs are explicit registrations, never guessed from class names or assembly scans.
        if (identity.Contains('/', StringComparison.Ordinal)) return null;
        try { return Type.GetType(identity, throwOnError: false, ignoreCase: true); }
        catch (TypeLoadException) { return null; }
        catch (System.IO.FileNotFoundException) { return null; }
        catch (System.IO.FileLoadException) { return null; }
    }

    static void RequireEventType(Type type)
    {
        if (!typeof(IEvent).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            throw new InvalidDataException($"Persisted type '{type.FullName}' is not a concrete event.");
    }
}
