using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Durable request for server-side preparation of broker snapshot evidence.</summary>
[MessagePackObject]
public sealed record BrokerAccountResynchronizationRequestedEvent : BrokerAccountOperationAppliedEvent
{
    public new const string Verb = "BrokerAccountResynchronizationRequested";
    [Key(10)] public DateTime RequestedAtUtc { get; init; }
    [IgnoreMember] public override string EventName => nameof(BrokerAccountResynchronizationRequestedEvent);
}
