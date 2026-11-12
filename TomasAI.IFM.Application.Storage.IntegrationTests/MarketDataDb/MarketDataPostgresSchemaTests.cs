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
        MarketDataSchemaDb.CreateStatement.Should().Contain("PRIMARY KEY (countryCode, monthBucket, eventDate, eventName)")
            .And.Contain("economic_calendar_ordered");
        MarketDataDbSql.GetEconomicCalendarCountryCodes.Should().Contain("LIMIT 512");
        MarketDataDbSql.GetEconomicCalendars.Should().Contain("FROM market_data.economic_calendar")
            .And.Contain("countrycode = $1").And.Contain("monthbucket = $2").And.Contain("LIMIT 2501");
    }

    [Fact]
    public void FmpSchemaPersistsSupplementalFieldsAndCalendarRejectsAtCanonicalRow()
    {
        MarketDataSchemaDb.CreateStatement.Should().Contain("impact text").And.Contain("unit text")
            .And.Contain("change text").And.Contain("changePercentage text")
            .And.Contain("market_data.market_data_import_ownership");
        MarketDataDbSql.InsertEconomicCalendarV2.Should().Contain("impact = EXCLUDED.impact")
            .And.Contain("changepercentage = EXCLUDED.changepercentage");
        MarketDataDbSql.GetEconomicCalendarV2CommandId.Should().Contain("commandid")
            .And.Contain("countrycode = $1 AND monthbucket = $2")
            .And.Contain("eventdate >= date_trunc('second', $3::timestamptz)")
            .And.Contain("eventdate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second'")
            .And.Contain("eventname = $4");
    }

    [Fact]
    public void AuxiliarySchemaAndQueriesUseAuthoritativeUnversionedTableNames()
    {
        foreach (var table in new[] { "market_data_import_ownership", "economic_calendar_country_code",
                     "futures_iti_timeframe_state", "yield_curve_rate_year", "yield_curve_rate_by_date" })
            MarketDataSchemaDb.CreateStatement.Should().Contain($"CREATE TABLE IF NOT EXISTS market_data.{table} (");
        MarketDataDbSql.GetFuturesItiTimeFrameState.Should().Contain("market_data.futures_iti_timeframe_state");
        MarketDataDbSql.GetYieldCurveRateYears.Should().Contain("market_data.yield_curve_rate_year");
        MarketDataDbSql.GetYieldCurveRates.Should().Contain("market_data.yield_curve_rate_by_date");
    }

    [Fact]
    public void YieldCurveQueriesUseOrderedAndDistinctBoundedShapes()
    {
        MarketDataSchemaDb.CreateStatement.Should().Contain("yield_curve_rates_ordered")
            .And.Contain("yield_curve_rate_by_date_ordered").And.Contain("yield_curve_rate_year_ordered");
        MarketDataDbSql.GetLastYieldCurveRate.Should().Contain("ORDER BY valuedate DESC LIMIT 1");
        MarketDataDbSql.GetYieldCurveRates.Should().Contain("LIMIT 5000");
        MarketDataDbSql.GetYieldCurveRateYears.Should().Contain("LIMIT 200")
            .And.Contain("ORDER BY rateyear DESC");
    }

    [Fact]
    public void EconomicCalendarSchemaAndRangeQueryUseCanonicalCountryMonthTable()
    {
        MarketDataSchemaDb.CreateStatement.Should().Contain("market_data.economic_calendar")
            .And.Contain("PRIMARY KEY (countryCode, monthBucket, eventDate, eventName)");
        MarketDataDbSql.GetEconomicCalendars.Should().Contain("countrycode = $1")
            .And.Contain("monthbucket = $2").And.Contain("eventdate >= $3").And.Contain("eventdate <= $4");
    }
}
