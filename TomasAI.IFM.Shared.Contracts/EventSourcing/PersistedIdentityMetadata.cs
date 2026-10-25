using System.Collections.Immutable;
using System.Text.Json;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Frozen metadata only: no dependency on business-contract assemblies or host startup.</summary>
internal static class PersistedIdentityMetadata
{
    internal static ImmutableDictionary<string, string> ReadTargets()
    {
        using var stream = typeof(PersistedIdentityMetadata).Assembly.GetManifestResourceStream("PersistedEventReadTargets.json")
            ?? throw new InvalidOperationException("Missing persisted event read-target metadata.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!.ToImmutableDictionary(StringComparer.Ordinal);
    }

    internal static ImmutableDictionary<string, EventWriteIdentity> ReadWrites()
    {
        using var stream = typeof(PersistedIdentityMetadata).Assembly.GetManifestResourceStream("PersistedEventWriteBaseline.json")
            ?? throw new InvalidOperationException("Missing persisted event write baseline.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().ToImmutableDictionary(
            row => row.GetProperty("currentType").GetString()!,
            row => new EventWriteIdentity(row.GetProperty("EventName").GetString()!, row.GetProperty("EventTypeName").GetString()!), StringComparer.Ordinal);
    }
}
