using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>Additive, versioned creation metadata. Existing MessagePack payloads remain unchanged.</summary>
public static class MessageOriginHeaders
{
    /// <summary>Versioned header shared by actor and non-actor transports.</summary>
    public const string Header = "Ifm-Message-Origin-V1";
    public const string BinaryHeader = "Ifm-Message-Origin-V2";

    /// <summary>Adds known metadata without allocating headers for an unknown legacy origin.</summary>
    public static NatsHeaders? Add(NatsHeaders? headers, MessageOrigin? origin)
    {
        if (origin is null) return headers;
        headers ??= new NatsHeaders();
        headers.Remove(Header);
        headers[BinaryHeader] = MessageOriginBinary.Serialize(origin);
        return headers;
    }

    /// <summary>Reads strict metadata; absent legacy metadata remains absent.</summary>
    public static MessageOrigin? Read(NatsHeaders? headers)
    {
        if (headers is null) return null;
        if (headers.TryGetValue(BinaryHeader, out var binary))
        {
            if (binary.Count != 1) throw new InvalidDataException("Duplicate V2 origin metadata.");
            var origin = MessageOriginBinary.Deserialize(binary.ToString());
            if (headers.TryGetValue(Header, out var legacy)
                && (legacy.Count != 1 || MessageOriginJson.Deserialize(legacy.ToString()) != origin))
                throw new InvalidDataException("Conflicting origin metadata versions.");
            return origin;
        }
        if (!headers.TryGetValue(Header, out var value)) return null;
        if (value.Count != 1 || value.ToString().Length > 2048)
            throw new InvalidDataException("Invalid message origin header.");
        return MessageOriginJson.Deserialize(value.ToString());
    }
}
