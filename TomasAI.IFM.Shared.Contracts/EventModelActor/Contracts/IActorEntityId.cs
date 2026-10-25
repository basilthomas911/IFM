using MessagePack;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents an interface for formatting entity identifiers into string representations.
/// </summary>
/// <remarks>This interface is designed to standardize the conversion of entity identifiers into string
/// representations that can be used as keys in dictionaries, databases, or other storage mechanisms.</remarks>
//#pragma warning disable MsgPack005 // Implementations carry [MessagePackObject]; no [Union] needed on the contract interface
[Union(0, typeof(ActorEntityId))]

public interface IActorEntityId
{
    /// <summary>
    /// Formats the specified entity identifier into a string representation.
    /// </summary>
    /// <returns>A string representation of the specified entity identifier.</returns>
    string Format();
}
