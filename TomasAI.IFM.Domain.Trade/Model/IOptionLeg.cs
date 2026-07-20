using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

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
