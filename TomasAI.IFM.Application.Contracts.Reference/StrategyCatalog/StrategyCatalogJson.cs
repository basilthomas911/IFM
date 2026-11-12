using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public static class StrategyCatalogJson
{
    static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // A command or stored version wraps the already depth-limited definition in one JSON object.
        MaxDepth = StrategyCatalogValidation.MaximumDefinitionDepth + 1
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > 524288)
            throw new ArgumentException("Catalog message is empty or exceeds the message limit.",nameof(json));
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new ArgumentException("Catalog message is null.",nameof(json));
    }
}
