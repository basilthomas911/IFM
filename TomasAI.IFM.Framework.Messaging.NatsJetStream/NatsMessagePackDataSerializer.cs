using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.Nats.Serializers;

/// <summary>
/// Provides functionality for serializing and deserializing objects using the MessagePack format.
/// </summary>
/// <remarks>This class leverages an underlying MessagePack-based binary serializer to handle serialization and
/// deserialization. It is designed to work with reference types and ensures compatibility with the MessagePack data
/// format.</remarks>
public class NatsMessagePackDataSerializer: IDataSerializer
{
    IBinarySerializer _serializer => new MessagePackBinarySerializer();

    /// <summary>
    /// Deserializes the specified byte array into an object of the specified type.
    /// </summary>
    /// <remarks>The method uses the underlying serializer to perform the deserialization. Ensure that the
    /// data format matches the expected format for the specified type.</remarks>
    /// <typeparam name="TData">The type of the object to deserialize. Must be a reference type.</typeparam>
    /// <param name="data">The byte array containing the serialized data to deserialize.</param>
    /// <returns>An instance of <typeparamref name="TData"/> if deserialization is successful; otherwise, <see langword="null"/>.</returns>
    public TData? Deserialize<TData>(byte[] data) where TData : class
        =>  _serializer.Deserialize<TData>(data);

    /// <summary>
    /// Deserializes the specified byte array into an object.
    /// </summary>
    /// <param name="data">The byte array containing the serialized object data. Cannot be null.</param>
    /// <returns>The deserialized object, or <see langword="null"/> if the data cannot be deserialized.</returns>
    public object? DeserializeObject(byte[] data)
        => _serializer.DeserializeObject(data);

    /// <summary>
    /// Serializes the specified object into a byte array.
    /// </summary>
    /// <typeparam name="TData">The type of the object to serialize. Must be a reference type.</typeparam>
    /// <param name="data">The object to serialize. Cannot be <see langword="null"/>.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    public byte[] Serialize<TData>(TData data) where TData : class
        => _serializer.Serialize(data)!;

    /// <summary>
    /// Serializes the specified object into a byte array.
    /// </summary>
    /// <param name="data">The object to serialize. Must not be <see langword="null"/>.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    public byte[] SerializeObject(object data)
        => _serializer.Serialize(data)!;
}
