namespace TomasAI.IFM.Framework.Messaging.Nats.Contracts;

/// <summary>
/// Defines methods for serializing and deserializing objects to and from binary data.
/// </summary>
/// <remarks>This interface provides a generic mechanism for converting objects to a binary representation and
/// reconstructing objects from binary data. It is designed to work with reference types.</remarks>
public interface IDataSerializer
{
    byte[] Serialize<TData>(TData data) where TData : class;
    TData? Deserialize<TData>(byte[] data) where TData : class;
}
