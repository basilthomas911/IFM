using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels;

public record CreditSpreadPricerArgs(
    int TradeId,
    TradeType TradeType,
    TradeStatus TradeStatus,
    DateOnly ValueDate,
    OptionStyle OptionStyle,
    OptionType OptionType,
    int DaysToMaturity,
    decimal AssetPrice,
    double RiskFreeRate,
    decimal ShortBid,
    decimal ShortAsk,
    double ShortStrike,
    double ShortImpliedVolatility,
    decimal LongBid,
    decimal LongAsk,
    double LongStrike,
    double LongImpliedVolatility,
    double RateOfReturn)
{
    public int LossFactor { get; set; }
}
