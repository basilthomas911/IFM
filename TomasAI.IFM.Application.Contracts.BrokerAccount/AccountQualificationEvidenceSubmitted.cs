using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record AccountQualificationEvidenceSubmittedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record AccountQualificationEvidenceSubmittedFailEvent : BrokerAccountOperationFail;
