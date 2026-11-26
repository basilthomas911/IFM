// Frozen before implementation: af6091d39. Benchmark-only baseline.
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;

using TomasAI.IFM.Framework.Messaging.Nats;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.Baselines;

/// <summary>Additive, versioned creation metadata. Existing MessagePack payloads remain unchanged.</summary>
public static class BaselineMessageOriginHeaders
{
    /// <summary>Versioned header shared by actor and non-actor transports.</summary>
    public const string Header = "Ifm-Message-Origin-V1";

    /// <summary>Adds known metadata without allocating headers for an unknown legacy origin.</summary>
    public static NatsHeaders? Add(NatsHeaders? headers, MessageOrigin? origin)
    {
        if (origin is null) return headers;
        headers ??= new NatsHeaders();
        headers[Header] = BaselineMessageOriginJson.Serialize(origin)!;
        return headers;
    }

    /// <summary>Reads strict metadata; absent legacy metadata remains absent.</summary>
    public static MessageOrigin? Read(NatsHeaders? headers)
    {
        if (headers is null || !headers.TryGetValue(Header, out var value)) return null;
        if (value.Count != 1 || value.ToString().Length > 2048)
            throw new InvalidDataException("Invalid message origin header.");
        return BaselineMessageOriginJson.Deserialize(value.ToString());
    }
}
