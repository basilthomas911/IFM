using NATS.Client.Core;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Lightweight wrapper around <see cref="NatsMsg{T}"/>. Zero-allocation value type.
/// </summary>
public readonly record struct RefNatsMsg(NatsMsg<byte[]> Message);
