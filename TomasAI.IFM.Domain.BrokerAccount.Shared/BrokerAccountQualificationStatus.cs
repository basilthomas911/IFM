using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the published broker message storage width and collection contract.")]
public enum BrokerAccountQualificationStatus : byte
{
    Unknown = 0,
    EvidencePending = 1,
    ReviewPending = 2,
    Accepted = 3,
    Revoked = 4
}
