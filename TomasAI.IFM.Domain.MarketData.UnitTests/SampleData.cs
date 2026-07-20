using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public static class SampleData
{
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = new(2024, 6, 15);
    public static readonly DateOnly StartDate = new(2024, 1, 1);
    public static readonly DateOnly EndDate = new(2024, 6, 30);
    public const MarketType Market = MarketType.Futures;
    public const CurrencyType Currency = CurrencyType.USD;

    public static RateOfReturnReadModel RateOfReturn = new(Symbol, ValueDate, 0.05);
    public static DateOnly[] TradingDates = [new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 3)];
}
