namespace TomasAI.IFM.Framework.Serialization;

/// <summary>
/// Defines methods for serializing and deserializing objects to and from binary data.
/// </summary>
/// <remarks>This interface provides generic and non-generic methods for serialization and deserialization.
/// Implementations of this interface are expected to handle the conversion of objects to binary representations and
/// vice versa, ensuring compatibility with the expected data format.</remarks>
public interface IBinarySerializer
{
    byte[]? Serialize<TData>(TData data);
    TData? Deserialize<TData>(byte[] data);
}
