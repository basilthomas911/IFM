using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Lightweight wrapper around <see cref="NatsMsg{T}"/>. Zero-allocation value type.
/// </summary>
public readonly record struct RefNatsMsg(NatsMsg<byte[]> Message);
