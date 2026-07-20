using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;

public interface IAlgorithmBuilder
{
    LongIronCondorAlgorithm BuildLongIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal);
    ShortIronCondorAlgorithm BuildShortIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal);
}
