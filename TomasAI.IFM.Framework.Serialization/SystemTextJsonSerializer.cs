using System.Text.Json;

namespace TomasAI.IFM.Framework.Serialization;

/// <summary>
/// Provides JSON serialization and deserialization functionality using System.Text.Json.
/// </summary>
/// <remarks>This class implements the <see cref="IJsonSerializer"/> interface to handle JSON data. It supports
/// various JSON content types and uses camel case naming policy by default.</remarks>
public class SystemTextJsonSerializer : IJsonSerializer
{
    static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Serialize(object obj) => JsonSerializer.Serialize(obj, _options);

    public T Deserialize<T>(string content) => JsonSerializer.Deserialize<T>(content, _options)!;

    public object Deserialize(string content, Type contentType) => JsonSerializer.Deserialize(content, contentType, _options)!;

    public string[] SupportedContentTypes { get; } = { "application/json", "text/json", "text/x-json", "text/javascript", "*+json" };

    public string ContentType { get; set; } = "application/json";

    public DataFormat DataFormat { get; } = DataFormat.Json;
}
