using FluentAssertions;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataPostgresSchemaTests
{
    [Fact]
    public void EconomicCalendarQueriesUseSingleBoundedCanonicalTable()
    {
        MarketDataSchemaSql.Create.Should().Contain("PRIMARY KEY (countryCode, monthBucket, eventDate, eventName)")
            .And.Contain("economic_calendar_ordered");
        MarketDataDbSql.GetEconomicCalendarCountryCodes.Should().Contain("LIMIT 512");
        MarketDataDbSql.GetEconomicCalendars.Should().Contain("FROM market_data.economic_calendar")
            .And.Contain("countryCode = $1").And.Contain("monthBucket = $2").And.Contain("LIMIT 2501");
    }

    [Fact]
    public void FmpSchemaPersistsSupplementalFieldsAndCalendarRejectsAtCanonicalRow()
    {
        MarketDataSchemaSql.Create.Should().Contain("impact text").And.Contain("unit text")
            .And.Contain("change text").And.Contain("changePercentage text")
            .And.Contain("market_data.market_data_import_ownership");
        MarketDataDbSql.InsertEconomicCalendarV2.Should().Contain("impact = EXCLUDED.impact")
            .And.Contain("changePercentage = EXCLUDED.changePercentage");
        MarketDataDbSql.GetEconomicCalendarV2CommandId.Should().Contain("commandId")
            .And.Contain("countryCode = $1 AND monthBucket = $2 AND eventDate = $3 AND eventName = $4");
    }

    [Fact]
    public void AuxiliarySchemaAndQueriesUseAuthoritativeUnversionedTableNames()
    {
        foreach (var table in new[] { "market_data_import_ownership", "economic_calendar_country_code",
                     "futures_iti_timeframe_state", "yield_curve_rate_year", "yield_curve_rate_by_date" })
            MarketDataSchemaSql.Create.Should().Contain($"CREATE TABLE IF NOT EXISTS market_data.{table} (");
        MarketDataDbSql.GetFuturesItiTimeFrameState.Should().Contain("market_data.futures_iti_timeframe_state");
        MarketDataDbSql.GetYieldCurveRateYears.Should().Contain("market_data.yield_curve_rate_year");
        MarketDataDbSql.GetYieldCurveRates.Should().Contain("market_data.yield_curve_rate_by_date");
    }

    [Fact]
    public void YieldCurveQueriesUseOrderedAndDistinctBoundedShapes()
    {
        MarketDataSchemaSql.Create.Should().Contain("yield_curve_rates_ordered")
            .And.Contain("yield_curve_rate_by_date_ordered").And.Contain("yield_curve_rate_year_ordered");
        MarketDataDbSql.GetLastYieldCurveRate.Should().Contain("ORDER BY valueDate DESC LIMIT 1");
        MarketDataDbSql.GetYieldCurveRates.Should().Contain("LIMIT 5000");
        MarketDataDbSql.GetYieldCurveRateYears.Should().Contain("LIMIT 200")
            .And.Contain("ORDER BY rateYear DESC");
    }

    [Fact]
    public void EconomicCalendarSchemaAndRangeQueryUseCanonicalCountryMonthTable()
    {
        MarketDataSchemaSql.Create.Should().Contain("market_data.economic_calendar")
            .And.Contain("PRIMARY KEY (countryCode, monthBucket, eventDate, eventName)");
        MarketDataDbSql.GetEconomicCalendars.Should().Contain("countryCode = $1")
            .And.Contain("monthBucket = $2").And.Contain("eventDate >= $3").And.Contain("eventDate <= $4");
    }
}
