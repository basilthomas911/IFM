using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record RevokeAccountQualificationCommand : BrokerAccountCommand
{
    public const string Verb = "RevokeAccountQualification";
    [Key(4)] public string Reason { get; init; } = string.Empty;
    [Key(5)] public string AuthorizedBy { get; init; } = string.Empty;
    [Key(6)] public DateTime RevokedAtUtc { get; init; }
}
