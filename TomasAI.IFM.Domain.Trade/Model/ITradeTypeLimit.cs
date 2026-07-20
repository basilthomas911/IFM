using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeTypeLimit
{
    int TradeId { get; }
    TradeType TradeType { get; }
    decimal MaxLossLimit { get; }
    decimal MinProfitLimit { get; }
    TradeTypeLimitReadModel ToViewModel();
}
