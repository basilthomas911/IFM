using FluentAssertions;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class ScyllaOptionVolatilityContractTests
{
    [Fact]
    public void HistoryQueriesUseExactPartitionKeysBoundedDatesAndNoFiltering()
    {
        OptionVolatilityCql.SelectObservationHistory.Should().Contain("calendar_bucket=:bucket")
            .And.Contain("value_date>=:from_date").And.Contain("value_date<=:to_date")
            .And.NotContain("ALLOW FILTERING");
        OptionVolatilityCql.SelectMetricHistory.Should().Contain("metric_policy_version=:policy")
            .And.Contain("calendar_bucket=:bucket").And.Contain("value_date>=:from_date")
            .And.Contain("value_date<=:to_date").And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public void LatestAdvanceUsesMonotonicConditionalFence()
    {
        OptionVolatilityCql.InsertLatest.Should().Contain("IF NOT EXISTS");
        OptionVolatilityCql.AdvanceLatest.Should().Contain("IF publication_sequence < :sequence");
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
        OptionVolatilityCql.InsertObservationHistory.Should().Contain("IF NOT EXISTS");
        OptionVolatilityCql.InsertMetricHistory.Should().Contain("IF NOT EXISTS");
        OptionVolatilityCql.InsertSnapshot.Should().Contain("IF NOT EXISTS");
    }
}
