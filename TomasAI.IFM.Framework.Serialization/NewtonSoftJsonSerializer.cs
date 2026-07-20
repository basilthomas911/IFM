using System;
using Newtonsoft.Json;


namespace TomasAI.IFM.Framework.Serialization;

/// <summary>
/// Provides JSON serialization and deserialization using the Newtonsoft.Json library.
/// </summary>
/// <remarks>This serializer supports multiple JSON-related content types and is compatible with the
/// Newtonsoft.Json (Json.NET) framework. It can be used to convert objects to and from JSON representations for HTTP
/// communication or data storage. The serializer is suitable for scenarios where flexible and widely supported JSON
/// handling is required.</remarks>
public class NewtonSoftJsonSerializer : IJsonSerializer
{
    public string Serialize(object obj) => JsonConvert.SerializeObject(obj, Formatting.None);

    public T Deserialize<T>(string content) => JsonConvert.DeserializeObject<T>(content)!;

    public object Deserialize(string content, Type contentType) => JsonConvert.DeserializeObject(content, contentType)!;

    public string[] SupportedContentTypes { get; } = { "application/json", "text/json", "text/x-json", "text/javascript", "*+json" };

    public string ContentType { get; set; } = "application/json";

    public DataFormat DataFormat { get; } = DataFormat.Json;
}
