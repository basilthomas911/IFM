using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record SubmitAccountQualificationEvidenceCommand : BrokerAccountCommand
{
    public const string Verb = "SubmitAccountQualificationEvidence";
    [Key(4)] public string ManifestHash { get; init; } = string.Empty;
    [Key(5)] public string EvidenceReference { get; init; } = string.Empty;
    [Key(6)] public DateTime SubmittedAtUtc { get; init; }
}
