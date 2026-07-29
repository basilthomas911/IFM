using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// 
/// </summary>
public interface IFundOrderTrade
{
    int OrderId { get; }
    int TradeId { get; }
    TradeState TradeState { get;  }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }

    FundOrderTradeReadModel ToViewModel();
    void SetTradeState(TradeState tradeState);
}
