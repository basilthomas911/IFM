using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
public static class IronCondorForwardTradePriceModel
{
    public static decimal Calculate(decimal currentValue, decimal? previousValue) => previousValue is null ? currentValue : currentValue + (currentValue - previousValue.Value);
}
