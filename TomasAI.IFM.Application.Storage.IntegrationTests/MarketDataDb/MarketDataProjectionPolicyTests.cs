using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataProjectionPolicyTests
{
    [Fact]
    public void MarketOutlookSnapshot_UsesOneUpsertRowPerDateAndLatestCutoffQuery()
    {
        Assert.Contains("market_data.market_outlook_snapshot", MarketDataSchemaSql.Create);
        Assert.Contains("ON CONFLICT (contractId, valueDate)", MarketDataDbSql.UpsertMarketOutlookSnapshot);
        Assert.Contains("valueDate <= $2", MarketDataDbSql.GetMarketOutlookSnapshot);
        Assert.Contains("ORDER BY valueDate DESC LIMIT 1", MarketDataDbSql.GetMarketOutlookSnapshot);
    }

    [Fact]
    public void FuturesEodSchema_PersistsMovingAveragesAndSupportsAdditiveUpgrade()
    {
        Assert.Contains("fiftyDMA numeric", MarketDataSchemaSql.Create);
        Assert.Contains("twoHundredDMA numeric", MarketDataSchemaSql.Create);
        Assert.Contains("fiftyDMA AS \"FiftyDMA\"", MarketDataDbSql.GetFuturesEodData);
        Assert.Contains("twoHundredDMA AS \"TwoHundredDMA\"", MarketDataDbSql.GetFuturesEodData);
    }

    [Fact]
    public void FuturesTradeSignalRepair_IsNonDestructiveAndHasQuarantineStorage()
    {
        Assert.Contains("market_data.futures_trade_signal_quarantine", MarketDataSchemaSql.Create);
        Assert.Contains("ON CONFLICT (fingerprint)", MarketDataDbSql.InsertFuturesTradeSignalQuarantine);
        Assert.Null(typeof(MarketDataDbSql).GetField("DeleteMalformedFuturesTradeSignal"));
    }

    [Fact]
    public void Vx_as_of_projection_uses_the_bounded_PostgreSQL_contract_read()
    {
        Assert.Contains("market_data.vix_futures_eod_data", MarketDataDbSql.GetLastVixFuturesEodData);
        Assert.Contains("contractId = $1 AND valueDate <= $2", MarketDataDbSql.GetLastVixFuturesEodData);
        Assert.Contains("ORDER BY valueDate DESC LIMIT 1", MarketDataDbSql.GetLastVixFuturesEodData);
    }

    [Fact]
    public void FuturesItiProjections_UseBoundedQueryShapedPartitions()
    {
        Assert.Contains("futures_iti_signal_ordered", MarketDataSchemaSql.Create);
        Assert.Contains("PRIMARY KEY (contractId, timePeriod, calendarBucketStart)", MarketDataSchemaSql.Create);
        Assert.Contains("timeFrameStartValueDate date", MarketDataSchemaSql.Create);
        Assert.Contains("bandPercentage double precision", MarketDataSchemaSql.Create);
        Assert.Contains("calendarBucketStart = $3", MarketDataDbSql.GetFuturesItiTimeFrameState);
    }

    [Fact]
    public void Market_data_schema_does_not_own_raw_tick_tables_or_a_Cql_catalog()
    {
        Assert.Null(typeof(MarketDataDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbCql"));
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS market_data.futures_tick_data (", MarketDataSchemaSql.Create);
    }

    [Fact]
    public void ScopedProjectionReadiness_FailsClosedWhileAnyOperationIsActive()
    {
        var ready = new MarketDataProjectionScopeStateReadModel(
            "projection", "scope", Guid.NewGuid(), IsReady: true, Blocked: false,
            ActiveOperationsEmpty: true);
        var active = ready with { ActiveOperationsEmpty = false };

        Assert.True(ready.CanRead);
        Assert.False(active.CanRead);
    }

    [Fact]
    public void Reconciliation_RejectsEqualCountsWithDifferentRowIdentities()
    {
        var result = new MarketDataProjectionBackfillReadModel(
            FuturesTicksSource: 1,
            FuturesTicksProjected: 1,
            FuturesTicksSourceFingerprint: "tick-source",
            FuturesTicksProjectedFingerprint: "tick-projection",
            FuturesEodRowsSource: 1,
            FuturesEodRowsProjected: 1,
            FuturesEodSourceFingerprint: "eod",
            FuturesEodProjectedFingerprint: "eod",
            VixFuturesEodRowsSource: 3,
            VixContractsSource: 2,
            VixContractsIndexed: 2,
            VixContractsSourceFingerprint: "vix",
            VixContractsIndexedFingerprint: "vix",
            FuturesItiSignalsSource: 1,
            FuturesItiSignalsByDayProjected: 1,
            FuturesItiSignalsByMonthProjected: 1,
            FuturesItiSignalsByTrendModeProjected: 1,
            FuturesItiSignalsSourceFingerprint: "iti",
            FuturesItiSignalsByDayFingerprint: "iti",
            FuturesItiSignalsByMonthFingerprint: "iti",
            FuturesItiSignalsByTrendModeFingerprint: "iti",
            CutoverCompleted: false);

        Assert.False(result.IsReconciled);
    }
}
