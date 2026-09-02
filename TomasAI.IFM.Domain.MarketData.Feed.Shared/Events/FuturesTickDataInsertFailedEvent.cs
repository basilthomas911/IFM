using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

public record FuturesTickDataInsertFailedEvent : ErrorEvent
{
    public FuturesContractV3ReadModel Contract { get; init; }
    public FuturesTickDataV2ReadModel[] TickData { get; init; }
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; }
}
