using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public static class SampleData
{
    static readonly DateTime EconomicEventDate = new(2025, 2, 15, 14, 30, 0);
    static readonly DateTime EconomicCalendarCreatedOn = new(2025, 1, 1, 0, 0, 0);
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = new(2024, 6, 15);
    public static readonly DateOnly StartDate = new(2024, 1, 1);
    public static readonly DateOnly EndDate = new(2024, 6, 30);
    public const MarketType Market = MarketType.Futures;
    public const CurrencyType Currency = CurrencyType.USD;

    public static RateOfReturnReadModel RateOfReturn = new(Symbol, ValueDate, 0.05);
    public static DateOnly[] TradingDates = [new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 3)];
    public static readonly EconomicCalendarReadModel EconomicCalendar = new(
        EconomicEventDate, "US", "Non-Farm Payrolls", "250K", "240K", "230K",
        EconomicCalendarCreatedOn, "admin");
}
