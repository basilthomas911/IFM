using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
public static class FuturesForwardTradePriceModel
{
    public static decimal Calculate(decimal currentValue, decimal? previousValue) => previousValue is null ? currentValue : currentValue + currentValue - previousValue.Value;
}
