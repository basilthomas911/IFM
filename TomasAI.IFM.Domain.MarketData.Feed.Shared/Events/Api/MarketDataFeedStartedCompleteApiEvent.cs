using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedStartedCompleteApiEvent : CompleteEvent
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public bool ResetStream { get; init; }
    public DateTime StartedOn { get; init; }
    public string StartedBy { get; init; }
}
