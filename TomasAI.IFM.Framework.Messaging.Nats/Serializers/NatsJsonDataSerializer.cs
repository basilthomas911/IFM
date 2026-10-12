using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

/// <summary>
/// Provides methods for serializing objects to JSON and deserializing JSON data to objects, using UTF-8 encoding for
/// byte array representation.
/// </summary>
/// <remarks>This class is designed to handle serialization and deserialization of data in JSON format. It uses
/// the Newtonsoft.Json library for JSON processing and UTF-8 encoding for byte array conversions.</remarks>
public class NatsJsonDataSerializer : IDataSerializer
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2326", Justification = "These settings only serialize the verified existing wire format; reads use separate settings that prohibit type activation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2327", Justification = "Serialization-only settings preserve the existing outgoing wire format. All reads use TypeNameHandling.None, verified by the compatibility tests.")]
    readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
    };

    static readonly JsonSerializerSettings ReadSettings = new() { TypeNameHandling = TypeNameHandling.None };

    /// <summary>
    /// Serializes the specified object to a JSON-formatted byte array.
    /// </summary>
    /// <typeparam name="TData">The type of the object to serialize. Must be a reference type.</typeparam>
    /// <param name="data">The object to serialize. Cannot be <see langword="null"/>.</param>
    /// <returns>A byte array containing the UTF-8 encoded JSON representation of the object. Returns an empty byte array if
    /// <paramref name="data"/> is <see langword="null"/> or if serialization fails.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Preserve the documented empty byte-array result for serialization failures.")]
    public byte[] Serialize<TData>(TData data) where TData : class
    {
        try
        {
            if (data is not null)
            {
                var json = JsonConvert.SerializeObject(data, Formatting.None, _settings);
                return Encoding.UTF8.GetBytes(json);
            }
        }
        catch
        {
            // Preserve the compatibility API's empty/null failure result.
        }
        return [];
    }

    /// <summary>
    /// Deserializes the specified byte array into an object of the specified type.
    /// </summary>
    /// <remarks>This method assumes the input data is encoded in UTF-8 format. If the input is null, empty,
    /// or if deserialization fails, the method returns <see langword="null"/>.</remarks>
    /// <typeparam name="TData">The type of the object to deserialize. Must be a reference type.</typeparam>
    /// <param name="data">The byte array containing the serialized JSON data. Cannot be null or empty.</param>
    /// <returns>An instance of <typeparamref name="TData"/> if deserialization is successful; otherwise, <see langword="null"/>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Preserve the documented null result for deserialization failures.")]
    public TData? Deserialize<TData>(byte[] data) where TData : class
    {
        try
        {
            if (data is not null && data.Length > 0)
            {
                var json = Encoding.UTF8.GetString(data);
                return JsonConvert.DeserializeObject<TData>(json, ReadSettings);
            }
        }
        catch
        {
            // Preserve the compatibility API's empty/null failure result.
        }
        return default;
    }
}
