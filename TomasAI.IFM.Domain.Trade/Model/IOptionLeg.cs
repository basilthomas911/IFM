using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface IOptionLeg
{
    int TradeId { get; }
    string ContractId { get; }
    int Quantity { get; }
    decimal StrikePrice { get; }
    OptionType OptionLegType { get; }
    OptionLegAction OptionLegAction { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }

    OptionTradeLegReadModel ToDataModel();
}
