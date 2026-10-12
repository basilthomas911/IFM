using FluentAssertions;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class PostgresOptionVolatilityContractTests
{
    static string Sql(string name) => (string)(typeof(MarketDataDbContext).Assembly
        .GetType("TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbSql")!
        .GetField(name)!.GetRawConstantValue() ?? throw new InvalidOperationException(name));

    [Fact]
    public void HistorySchemasIndexSeriesDatesAndRevisions()
    {
        MarketDataSchemaSql.Create.Should().Contain("option_iv_observation_history_ordered")
            .And.Contain("option_iv_metric_history_ordered")
            .And.Contain("value_date ASC,sampling_slot ASC,revision DESC")
            .And.Contain("calendar_bucket int");
    }

    [Fact]
    public void LatestAdvanceUsesMonotonicConflictFence()
    {
        Sql("InsertLatest").Should().Contain("ON CONFLICT (environment,series_id,methodology_version,metric_policy_version)")
            .And.Contain("WHERE market_data.option_iv_latest.publication_sequence < EXCLUDED.publication_sequence");
        MarketDataSchemaSql.Create.Should().Contain("PRIMARY KEY (environment,series_id,methodology_version,metric_policy_version)");
    }

    [Fact]
    public void EvidenceUpsertsOnlyAcceptIdenticalPayloads()
    {
        foreach (var name in new[] { "InsertObservationById", "InsertObservationHistory", "InsertMetricHistory", "InsertSnapshot" })
            Sql(name).Should().Contain("DO UPDATE SET payload=EXCLUDED.payload WHERE")
                .And.Contain(".payload=EXCLUDED.payload RETURNING 1;");
    }
}
