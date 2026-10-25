using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record AcceptAccountQualificationCommand : BrokerAccountCommand
{
    public const string Verb = "AcceptAccountQualification";
    [Key(4)] public Guid ApprovalId { get; init; }
    [Key(5)] public string ManifestHash { get; init; } = string.Empty;
    [Key(6)] public string AuthorizedBy { get; init; } = string.Empty;
    [Key(7)] public DateTime ReviewedAtUtc { get; init; }
}
