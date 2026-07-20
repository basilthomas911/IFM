using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines methods for serializing and deserializing objects to and from a binary format.
/// </summary>
/// <remarks>Implementations of this interface enable conversion between objects and their binary representations,
/// which can be used for storage, transmission, or interoperability with other systems. The specific serialization
/// format is determined by the implementation.</remarks>
public interface IDataSerializer
{
    byte[] Serialize<TData>(TData data) where TData : class;
    TData? Deserialize<TData>(byte[] data) where TData : class;
}

