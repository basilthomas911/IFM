using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

public record FuturesTickDataInsertFailedEvent : ErrorEvent
{
    public FuturesContractV2ReadModel Contract { get; init; }
    public FuturesTickDataV2ReadModel[] TickData { get; init; }
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; }
}
