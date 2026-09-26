using FluentAssertions;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class ScyllaOptionVolatilityContractTests
{
    static string Cql(string name)
        => (string)(typeof(MarketDataDbContext).Assembly
            .GetType("TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbCql")!
            .GetField(name)!
            .GetRawConstantValue()
            ?? throw new InvalidOperationException($"Missing CQL constant {name}."));

    [Fact]
    public void HistoryQueriesUseExactPartitionKeysBoundedDatesAndNoFiltering()
    {
        Cql("SelectObservationHistory").Should().Contain("calendar_bucket=:bucket")
            .And.Contain("value_date>=:from_date").And.Contain("value_date<=:to_date")
            .And.NotContain("ALLOW FILTERING");
        Cql("SelectMetricHistory").Should().Contain("metric_policy_version=:policy")
            .And.Contain("calendar_bucket=:bucket").And.Contain("value_date>=:from_date")
            .And.Contain("value_date<=:to_date").And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public void LatestAdvanceUsesMonotonicConditionalFence()
    {
        Cql("InsertLatest").Should().Contain("IF NOT EXISTS");
        Cql("AdvanceLatest").Should().Contain("IF publication_sequence < :sequence");
        OptionVolatilitySchemaCql.CreateLatest.Should()
            .Contain("PRIMARY KEY((environment,series_id,methodology_version,metric_policy_version))");
    }

    [Fact]
    public void EvidenceSchemasAreAppendOnlyAndMonthlyPartitioned()
    {
        OptionVolatilitySchemaCql.CreateObservationHistory.Should()
            .Contain("calendar_bucket int").And.Contain("revision int")
            .And.Contain("PRIMARY KEY((environment,series_id,methodology_version,calendar_bucket)");
        OptionVolatilitySchemaCql.CreateMetricHistory.Should()
            .Contain("PRIMARY KEY((environment,series_id,methodology_version,metric_policy_version,calendar_bucket)")
            .And.Contain("revision int");
        Cql("InsertObservationHistory").Should().Contain("IF NOT EXISTS");
        Cql("InsertMetricHistory").Should().Contain("IF NOT EXISTS");
        Cql("InsertSnapshot").Should().Contain("IF NOT EXISTS");
    }
}
