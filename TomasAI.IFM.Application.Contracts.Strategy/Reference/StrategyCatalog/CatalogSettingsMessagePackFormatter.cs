using System.Text.Json;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Encodes the open-ended catalog settings object as JSON inside a typed MessagePack definition.</summary>
public sealed class CatalogSettingsMessagePackFormatter : IMessagePackFormatter<JsonElement>
{
    public void Serialize(ref MessagePackWriter writer, JsonElement value, MessagePackSerializerOptions options)
        => writer.Write(value.GetRawText());

    public JsonElement Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var json = reader.ReadString() ?? throw new InvalidDataException("Catalog settings are missing.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            MaxDepth = StrategyCatalogValidation.MaximumDefinitionDepth
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Catalog settings must be a JSON object.");
        return document.RootElement.Clone();
    }
}
