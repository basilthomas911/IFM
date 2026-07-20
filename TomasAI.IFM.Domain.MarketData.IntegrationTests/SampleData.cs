using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public static class SampleData
{
    public const string Symbol = "ES";
    public static readonly DateOnly ValueDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public static RateOfReturnReadModel RateOfReturn => new(Symbol, ValueDate, 0.05);

    public static MarketHolidayReadModel MarketHoliday => new(CurrencyType.USD, new DateOnly(2025, 7, 4), "Independence Day");
}
