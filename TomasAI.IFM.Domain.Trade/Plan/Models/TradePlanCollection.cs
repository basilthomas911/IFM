using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan.Models;

public class TradePlanCollection : Dictionary<TradePlanEntityId, TradePlanReadModel>
{
}
