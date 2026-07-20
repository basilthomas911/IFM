using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeLiveFeed
{
    int OrderId { get; }
    int TradeId { get; }
    bool LiveFeed { get; }
    TradeLiveFeedReadModel ToViewModel();
}
