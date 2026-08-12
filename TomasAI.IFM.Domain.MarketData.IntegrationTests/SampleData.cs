using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public static class SampleData
{
    static readonly DateTime EconomicCalendarCreatedOn = new(2025, 1, 1);
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public static RateOfReturnReadModel RateOfReturn => new(Symbol, ValueDate, 0.05);

    public static MarketHolidayReadModel MarketHoliday => new(CurrencyType.USD, new DateOnly(2025, 7, 4), "Independence Day");

    public static readonly EconomicCalendarReadModel EconomicCalendar1 = Calendar(new(2025, 2, 15, 14, 30, 0), "US", "Non-Farm Payrolls", "250K", "240K", "230K");
    public static readonly EconomicCalendarReadModel EconomicCalendar2 = Calendar(new(2025, 3, 20, 10, 0, 0), "US", "GDP Growth Rate", "2.5%", "2.3%", "2.1%");
    public static readonly EconomicCalendarReadModel EconomicCalendar3 = Calendar(new(2025, 4, 10, 9, 0, 0), "EU", "ECB Interest Rate Decision", "4.0%", "4.0%", "3.75%");
    public static readonly EconomicCalendarReadModel EconomicCalendar4 = Calendar(new(2025, 5, 12, 8, 30, 0), "US", "Consumer Price Index", "3.2%", "3.1%", "3.0%");
    public static readonly EconomicCalendarReadModel EconomicCalendar5 = Calendar(new(2025, 6, 18, 7, 0, 0), "GB", "Retail Sales", "1.5%", "1.2%", "0.8%");
    public static readonly EconomicCalendarReadModel[] EconomicCalendars = [EconomicCalendar1, EconomicCalendar2, EconomicCalendar3, EconomicCalendar4, EconomicCalendar5];

    static EconomicCalendarReadModel Calendar(DateTime date, string country, string name, string actual, string forecast, string prior)
        => new(date, country, name, actual, forecast, prior, EconomicCalendarCreatedOn, "IntegrationTest");
}
