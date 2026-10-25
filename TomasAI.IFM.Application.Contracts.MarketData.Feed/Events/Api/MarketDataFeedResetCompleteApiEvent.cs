using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedResetCompleteApiEvent : CompleteEvent
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public DateTime ResetOn { get; init; }
    public string ResetBy { get; init; }
}
