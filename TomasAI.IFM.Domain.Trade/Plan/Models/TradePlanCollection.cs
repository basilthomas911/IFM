using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan.Models;

public class TradePlanCollection : Dictionary<TradePlanEntityId, TradePlanReadModel>
{
}
