using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record AccountQualificationRevokedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record AccountQualificationRevokedFailEvent : BrokerAccountOperationFail;
