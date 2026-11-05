using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Records a no-op or a business rejection without changing account state.</summary>
[MessagePackObject]
public sealed record BrokerAccountOperationDecidedEvent : BrokerAccountOperationAppliedEvent
{
    public new const string Verb = "BrokerAccountOperationDecided";
    [Key(10)] public bool Accepted { get; init; }
    [Key(11)] public int RejectionCode { get; init; }
    [Key(12)] public string RejectionReason { get; init; } = string.Empty;
    [IgnoreMember] public override string EventName => nameof(BrokerAccountOperationDecidedEvent);
}
