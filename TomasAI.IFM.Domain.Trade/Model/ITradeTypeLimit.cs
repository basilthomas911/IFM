using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeTypeLimit
{
    int TradeId { get; }
    TradeType TradeType { get; }
    decimal MaxLossLimit { get; }
    decimal MinProfitLimit { get; }
    TradeTypeLimitReadModel ToViewModel();
}
