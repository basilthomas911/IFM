using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public static class OptionLegReadModelExtension
{
    public static OptionLeg ToOptionLeg(this OptionTradeLegReadModel e, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new(orderId: e.OrderId, tradeId: e.TradeId, contractId: e.ContractId, quantity: e.Quantity, strikePrice: e.StrikePrice, optionLegType: e.OptionLegType, optionLegAction: e.OptionLegAction, createdOn: createdOn, createdBy: createdBy, updatedOn: updatedOn, updatedBy: updatedBy);
    }
}
