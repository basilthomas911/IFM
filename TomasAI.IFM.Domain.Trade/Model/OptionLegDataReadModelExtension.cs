using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using MathNet.Numerics;

namespace TomasAI.IFM.Domain.Trade.Model;
public static class OptionLegDataReadModelExtension
{
    public static OptionLegData ToOptionLegData(this OptionTradeLegDataReadModel e, TradePositionEntityId tradeDataKey, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) => new(key: tradeDataKey, model: e, createdOn: createdOn, createdBy: createdBy, updatedOn: updatedOn, updatedBy: updatedBy);
}
