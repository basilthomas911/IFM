using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeLiveFeed
{
    int OrderId { get; }
    int TradeId { get; }
    bool LiveFeed { get; }
    TradeLiveFeedReadModel ToViewModel();
}
